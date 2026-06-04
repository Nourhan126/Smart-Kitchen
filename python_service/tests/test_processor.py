"""
tests/test_processor.py
-----------------------
Unit tests for the image processor module.
"""

import io
import sys
from pathlib import Path

import pytest
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from app.processor import process_and_save
from app.config import IMAGE_SIZE


def _make_jpeg(width: int, height: int) -> bytes:
    img = Image.new("RGB", (width, height), color=(180, 90, 45))
    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=85)
    return buf.getvalue()


def test_square_image_processed(tmp_path):
    data = _make_jpeg(400, 400)
    dest = tmp_path / "out.jpg"
    ok = process_and_save(data, dest)
    assert ok
    assert dest.exists()
    with Image.open(dest) as img:
        assert img.size == IMAGE_SIZE
        assert img.mode == "RGB"


def test_landscape_image_cropped_and_resized(tmp_path):
    data = _make_jpeg(800, 400)
    dest = tmp_path / "out.jpg"
    ok = process_and_save(data, dest)
    assert ok
    with Image.open(dest) as img:
        assert img.size == IMAGE_SIZE


def test_portrait_image_cropped_and_resized(tmp_path):
    data = _make_jpeg(400, 800)
    dest = tmp_path / "out.jpg"
    ok = process_and_save(data, dest)
    assert ok
    with Image.open(dest) as img:
        assert img.size == IMAGE_SIZE


def test_corrupt_bytes_returns_false(tmp_path):
    dest = tmp_path / "out.jpg"
    ok = process_and_save(b"garbage data", dest)
    assert not ok
    assert not dest.exists()
