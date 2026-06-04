"""
progress.py
-----------
Thread-safe, file-backed progress tracker.

State is persisted as a JSON file so that bulk processing can be resumed
after a crash, restart, or deployment update without re-processing recipes
that already have images.

Schema of progress.json
────────────────────────
{
    "completed": {
        "<recipe_slug>": {
            "image_path": "dataset/images/pizza.jpg",
            "image_url": "https://...",
            "timestamp": "2024-01-01T12:00:00"
        }
    },
    "failed": {
        "<recipe_slug>": {
            "error": "no valid image found",
            "attempts": 3,
            "timestamp": "..."
        }
    }
}
"""

import csv
import json
import logging
import threading
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional


logger = logging.getLogger(__name__)


def _now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def _slugify(name: str) -> str:
    """Convert a recipe name to a safe filesystem / dict key."""
    return (
        name.lower()
        .replace(" ", "_")
        .replace("/", "_")
        .replace("\\", "_")
        .replace("'", "")
        .replace('"', "")
        [:200]  # cap length
    )


class ProgressTracker:
    """
    Persists download progress to disk so that bulk jobs are resumable.

    Thread-safe: all public methods acquire an internal lock before
    reading or writing the in-memory state and flushing to disk.
    """

    def __init__(self, progress_file: Path) -> None:
        self._file = progress_file
        self._lock = threading.Lock()
        self._state: dict = {"completed": {}, "failed": {}}
        self._load()

    # ── Public interface ──────────────────────────────────────────────────────

    def is_done(self, recipe_name: str) -> bool:
        """Return True if the recipe already has a successfully downloaded image."""
        with self._lock:
            return _slugify(recipe_name) in self._state["completed"]

    def mark_completed(
        self,
        recipe_name: str,
        image_path: str,
        image_url: str,
    ) -> None:
        slug = _slugify(recipe_name)
        with self._lock:
            self._state["completed"][slug] = {
                "recipe_name": recipe_name,
                "image_path": image_path,
                "image_url": image_url,
                "timestamp": _now_iso(),
            }
            # Remove from failed if it was previously attempted
            self._state["failed"].pop(slug, None)
            self._flush()
            self._append_csv(recipe_name, image_path, image_url)

    def mark_failed(self, recipe_name: str, error: str) -> None:
        slug = _slugify(recipe_name)
        with self._lock:
            existing = self._state["failed"].get(slug, {})
            self._state["failed"][slug] = {
                "recipe_name": recipe_name,
                "error": error,
                "attempts": existing.get("attempts", 0) + 1,
                "timestamp": _now_iso(),
            }
            self._flush()

    def get_completed_entry(self, recipe_name: str) -> Optional[dict]:
        with self._lock:
            return self._state["completed"].get(_slugify(recipe_name))

    @property
    def completed_count(self) -> int:
        with self._lock:
            return len(self._state["completed"])

    @property
    def failed_count(self) -> int:
        with self._lock:
            return len(self._state["failed"])

    def completed_slugs(self) -> set:
        with self._lock:
            return set(self._state["completed"].keys())

    # ── Internal helpers ──────────────────────────────────────────────────────

    def _load(self) -> None:
        if self._file.exists():
            try:
                with self._file.open("r", encoding="utf-8") as fh:
                    loaded = json.load(fh)
                    self._state["completed"] = loaded.get("completed", {})
                    self._state["failed"] = loaded.get("failed", {})
                logger.info(
                    "Loaded progress: %d completed, %d failed",
                    len(self._state["completed"]),
                    len(self._state["failed"]),
                )
            except (json.JSONDecodeError, OSError) as exc:
                logger.warning("Could not load progress file (%s) – starting fresh.", exc)

    def _flush(self) -> None:
        """Write current state to disk atomically via a temporary file."""
        tmp = self._file.with_suffix(".tmp")
        try:
            with tmp.open("w", encoding="utf-8") as fh:
                json.dump(self._state, fh, ensure_ascii=False, indent=2)
            tmp.replace(self._file)
        except OSError as exc:
            logger.error("Failed to persist progress: %s", exc)

    def _append_csv(
        self,
        recipe_name: str,
        image_path: str,
        image_url: str,
    ) -> None:
        try:
            output_csv = self._file.parent / "recipes_with_images.csv"
            output_csv.parent.mkdir(parents=True, exist_ok=True)
            write_header = not output_csv.exists()
            with output_csv.open("a", encoding="utf-8", newline="") as fh:
                writer = csv.writer(fh)
                if write_header:
                    writer.writerow([
                        "recipe_name",
                        "image_path",
                        "image_url",
                        "local_image_name",
                    ])
                writer.writerow([
                    recipe_name,
                    image_path,
                    image_url,
                    Path(image_path).name,
                ])
        except OSError as exc:
            logger.error("Failed to append CSV metadata: %s", exc)
