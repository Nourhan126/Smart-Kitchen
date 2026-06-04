"""
pipeline.py
Production-ready image pipeline using DDGS instead of broken Selenium selectors.
"""

import asyncio
import csv
import io
import logging
import re
from pathlib import Path
from typing import AsyncIterator, List, Optional, Tuple

from ddgs import DDGS

from app.config import (
    BATCH_SIZE,
    IMAGES_DIR,
    MAX_RESULTS_TO_TRY,
    PROGRESS_FILE,
    SEARCH_QUERY_SUFFIX,
)

from app.downloader import download_image
from app.models import SingleSearchResponse
from app.processor import process_and_save
from app.progress import ProgressTracker
from app.validator import validate_image_bytes

logger = logging.getLogger(__name__)

# Shared progress tracker
_tracker: Optional[ProgressTracker] = None


def get_tracker() -> ProgressTracker:
    global _tracker

    if _tracker is None:
        _tracker = ProgressTracker(PROGRESS_FILE)

    return _tracker


# ───────────────────────────────────────────────────────
# DDGS IMAGE SEARCH
# ───────────────────────────────────────────────────────

def search_google_images(
    recipe_name: str,
    target_type: str = "recipe",
    context: Optional[str] = None,
) -> List[str]:
    """
    Search food images using DDGS.
    """

    query = build_search_query(recipe_name, target_type, context)

    try:
        with DDGS() as ddgs:

            results = list(
                ddgs.images(
                    query,
                    max_results=MAX_RESULTS_TO_TRY * 8
                )
            )

        urls = []

        for r in results:

            image_url = r.get("image")

            if image_url and _is_candidate_url(image_url):
                urls.append(image_url)

        logger.info(f"Found {len(urls)} images for: {recipe_name}")

        return list(dict.fromkeys(urls))[:MAX_RESULTS_TO_TRY]

    except Exception as e:

        logger.error(f"DDGS failed for {recipe_name}: {e}")

        return []


# ───────────────────────────────────────────────────────
# SINGLE SEARCH
# ───────────────────────────────────────────────────────

async def search_and_download(
    recipe_name: str,
    target_type: str = "recipe",
    context: Optional[str] = None,
) -> SingleSearchResponse:

    tracker = get_tracker()
    target_type = normalize_target_type(target_type)
    progress_key = f"{target_type}:{recipe_name}:{context or ''}"

    # Skip completed
    if tracker.is_done(progress_key):

        entry = tracker.get_completed_entry(progress_key)

        return SingleSearchResponse(
            recipe_name=recipe_name,
            target_type=target_type,
            image_path=entry["image_path"],
            image_url=entry["image_url"],
            success=True,
        )

    # Search images
    candidate_urls = search_google_images(recipe_name, target_type, context)

    if not candidate_urls:

        tracker.mark_failed(progress_key, "no candidate URLs found")

        return SingleSearchResponse(
            recipe_name=recipe_name,
            target_type=target_type,
            success=False,
            error="no candidate URLs found",
        )

    # Save destination
    dest_path = IMAGES_DIR / target_type / _safe_filename(
        f"{recipe_name} {context or ''}"
    )

    # Try candidate images
    for url in candidate_urls:

        try:

            print(f"Trying image: {url}")

            # Download image
            data = await download_image(url)

            if data is None:
                continue

            valid, reason = validate_image_bytes(data)

            if not valid:
                logger.info(f"Rejected image for {recipe_name}: {reason}")
                continue

            saved = process_and_save(data, dest_path)

            if not saved:
                continue

            # Relative path
            rel_path = str(
                dest_path.relative_to(
                    dest_path.parent.parent.parent
                )
            )

            # Save progress
            tracker.mark_completed(
                progress_key,
                rel_path,
                url
            )

            logger.info(f"SUCCESS: {recipe_name}")

            return SingleSearchResponse(
                recipe_name=recipe_name,
                target_type=target_type,
                image_path=rel_path,
                image_url=url,
                success=True,
            )

        except Exception as e:

            logger.error(f"Failed URL {url}: {e}")

            continue

    # All failed
    error_msg = "all candidate images failed"

    tracker.mark_failed(progress_key, error_msg)

    return SingleSearchResponse(
        recipe_name=recipe_name,
        target_type=target_type,
        success=False,
        error=error_msg,
    )


# ───────────────────────────────────────────────────────
# CSV PARSER
# ───────────────────────────────────────────────────────

def parse_recipe_names_from_csv(file_bytes: bytes) -> List[str]:

    text = file_bytes.decode("utf-8", errors="replace")

    reader = csv.DictReader(io.StringIO(text))

    headers = [h.lower().strip() for h in (reader.fieldnames or [])]

    name_col = None

    for candidate in ("name", "recipe_name", "title", "recipe"):

        if candidate in headers:

            name_col = reader.fieldnames[
                headers.index(candidate)
            ]

            break

    names = []

    for row in reader:

        if name_col:
            val = row.get(name_col, "").strip()

        else:
            val = next(iter(row.values()), "").strip()

        if val:
            names.append(val)

    return names


# ───────────────────────────────────────────────────────
# BULK PROCESSING
# ───────────────────────────────────────────────────────

async def bulk_process(
    recipe_names: List[str],
) -> AsyncIterator[Tuple[int, int, SingleSearchResponse]]:

    tracker = get_tracker()

    total = len(recipe_names)

    pending = [
    n for n in recipe_names
    if not tracker.is_done(f"recipe:{n}:")
]
    already_done = total - len(pending)

    logger.info(
        f"Bulk job: {total} total | "
        f"{already_done} done | "
        f"{len(pending)} pending"
    )

    processed = already_done

    for batch_start in range(0, len(pending), BATCH_SIZE):

        batch = pending[
            batch_start: batch_start + BATCH_SIZE
        ]

        tasks = [
            search_and_download(name, "recipe")
            for name in batch
        ]

        results = await asyncio.gather(
            *tasks,
            return_exceptions=True
        )

        for name, result in zip(batch, results):

            processed += 1

            if isinstance(result, Exception):

                tracker.mark_failed(name, str(result))

                result = SingleSearchResponse(
                    recipe_name=name,
                    target_type="recipe",
                    success=False,
                    error=str(result)
                )

            yield processed, total, result

        # Delay between batches
        await asyncio.sleep(1.5)


# ───────────────────────────────────────────────────────
# HELPERS
# ───────────────────────────────────────────────────────

def _safe_filename(recipe_name: str) -> str:

    safe = re.sub(
        r"[^\w\s-]",
        "",
        recipe_name.lower()
    )

    safe = re.sub(
        r"[\s]+",
        "_",
        safe.strip()
    )

    return f"{safe[:200]}.jpg"


def normalize_target_type(target_type: str) -> str:
    value = (target_type or "recipe").strip().lower()
    if value not in {"recipe", "ingredient", "step"}:
        return "recipe"
    return value


def build_search_query(
    name: str,
    target_type: str,
    context: Optional[str] = None,
) -> str:

    target_type = normalize_target_type(target_type)

    if target_type == "ingredient":

        return (
            f'"{name}" '
            f'ingredient food ingredient photography '
            f'isolated ingredient'
        )

    if target_type == "step":

        parts = [
            f'"{name}"'
        ]

        if context:
            parts.append(context)

        parts.append(
            "cooking step recipe preparation food"
        )

        return " ".join(parts)

    return (
        f'"{name}" '
        f'recipe finished dish plated food '
        f'food photography'
    )


def _is_candidate_url(url: str) -> bool:
    if not url.startswith("http"):
        return False

    lowered = url.lower()
    blocked_terms = (
        "logo",
        "avatar",
        "profile",
        "portrait",
        "headshot",
        "people",
        "person",
        "banner",
        "ad_",
        "advert",
        "menu",
        "infographic",
        "icon",
        "sprite",
    )
    return not any(term in lowered for term in blocked_terms)
