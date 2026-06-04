"""
tests/test_progress.py
----------------------
Unit tests for the ProgressTracker.
"""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from app.progress import ProgressTracker


def test_mark_completed(tmp_path):
    tracker = ProgressTracker(tmp_path / "progress.json")
    tracker.mark_completed("pizza", "dataset/images/pizza.jpg", "https://example.com/img.jpg")
    assert tracker.is_done("pizza")
    assert tracker.completed_count == 1
    assert tracker.failed_count == 0


def test_mark_failed(tmp_path):
    tracker = ProgressTracker(tmp_path / "progress.json")
    tracker.mark_failed("burger", "no image found")
    assert not tracker.is_done("burger")
    assert tracker.failed_count == 1


def test_resume_from_file(tmp_path):
    p = tmp_path / "progress.json"
    t1 = ProgressTracker(p)
    t1.mark_completed("pasta", "dataset/images/pasta.jpg", "https://example.com/pasta.jpg")

    # Create a new tracker loading from the same file
    t2 = ProgressTracker(p)
    assert t2.is_done("pasta")
    assert t2.completed_count == 1


def test_case_insensitive_slug(tmp_path):
    tracker = ProgressTracker(tmp_path / "progress.json")
    tracker.mark_completed("Pesto Pizza", "img.jpg", "url")
    assert tracker.is_done("Pesto Pizza")
    assert tracker.is_done("pesto pizza")


def test_completed_overrides_failed(tmp_path):
    tracker = ProgressTracker(tmp_path / "progress.json")
    tracker.mark_failed("tacos", "timeout")
    tracker.mark_completed("tacos", "img.jpg", "url")
    assert tracker.is_done("tacos")
    assert tracker.failed_count == 0
