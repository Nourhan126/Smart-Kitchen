"""
tests/test_validator.py
-----------------------
Unit tests for the image validator module.
Run with: pytest python_service/tests/ -v
"""

import io
import pytest
from PIL import Image
import numpy as np

# Make sure tests can import from app/
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from app.validator import validate_image_bytes, reset_duplicate_registry


def _make_jpeg(width: int, height: int, color=(120, 80, 50)) -> bytes:
    """Create a minimal JPEG in memory with some color variation so it passes
    the variance check in the validator."""
    img = Image.new("RGB", (width, height), color=color)
    # Add a contrasting rectangle in the corner to ensure colour variance
    pixels = img.load()
    contrast = (255 - color[0], 255 - color[1], 255 - color[2])
    patch = max(10, width // 8), max(10, height // 8)
    for x in range(patch[0]):
        for y in range(patch[1]):
            pixels[x, y] = contrast
    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=85)
    return buf.getvalue()


def setup_function():
    # Reset duplicate registry before each test function
    reset_duplicate_registry()


# ── Basic validity ────────────────────────────────────────────────────────────

def test_valid_image_passes():
    data = _make_jpeg(300, 300)
    ok, reason = validate_image_bytes(data)
    assert ok, f"Expected pass but got: {reason}"


def test_empty_bytes_rejected():
    ok, reason = validate_image_bytes(b"")
    assert not ok
    assert "empty" in reason


def test_corrupt_bytes_rejected():
    ok, reason = validate_image_bytes(b"not an image at all!!")
    assert not ok


# ── Size checks ───────────────────────────────────────────────────────────────

def test_tiny_image_rejected():
    data = _make_jpeg(50, 50)
    ok, reason = validate_image_bytes(data)
    assert not ok
    assert "small" in reason


def test_minimum_dimension_image_passes():
    from app.config import MIN_IMAGE_DIM
    data = _make_jpeg(MIN_IMAGE_DIM, MIN_IMAGE_DIM)
    ok, _ = validate_image_bytes(data)
    assert ok


# ── Aspect ratio ──────────────────────────────────────────────────────────────

def test_extreme_aspect_ratio_rejected():
    # Banner-style image: very wide but tall enough to pass the size check
    data = _make_jpeg(2000, 200)
    ok, reason = validate_image_bytes(data)
    assert not ok
    assert "aspect" in reason


# ── Duplicate detection ───────────────────────────────────────────────────────

def test_duplicate_rejected():
    # Use a colorful image that passes all other checks
    data = _make_jpeg(300, 300, color=(30, 150, 80))
    ok1, _ = validate_image_bytes(data)
    assert ok1

    ok2, reason2 = validate_image_bytes(data)
    assert not ok2
    assert "duplicate" in reason2


def test_different_images_not_duplicates():
    reset_duplicate_registry()
    data1 = _make_jpeg(300, 300, color=(200, 100, 50))
    data2 = _make_jpeg(300, 300, color=(50, 200, 100))
    ok1, _ = validate_image_bytes(data1)
    ok2, _ = validate_image_bytes(data2)
    assert ok1
    assert ok2
