"""
processor.py
------------
Image processing utilities: center-crop, aspect-ratio-preserving resize,
and RGB normalisation.

All saved images are:
  - RGB colour space
  - Center-cropped to a square before resizing (preserves the most relevant
    content, avoids squashing)
  - Resized to IMAGE_SIZE (default 256×256)
  - Saved as high-quality JPEG
"""

import io
import logging
from pathlib import Path

from PIL import Image

from app.config import IMAGE_SIZE

logger = logging.getLogger(__name__)


def process_and_save(data: bytes, dest_path: Path) -> bool:
    """
    Process raw image bytes and write the result to *dest_path*.

    Processing steps
    ────────────────
    1. Decode with PIL
    2. Convert to RGB (handles RGBA, P, L modes transparently)
    3. Center-crop to the largest possible square
    4. Resize to IMAGE_SIZE with high-quality LANCZOS resampling
    5. Save as JPEG (quality=90)

    Returns True on success, False on any error.
    """
    try:
        img = Image.open(io.BytesIO(data))
        img = img.convert("RGB")
        img = _center_crop(img)
        img = img.resize(IMAGE_SIZE, Image.LANCZOS)
        dest_path.parent.mkdir(parents=True, exist_ok=True)
        img.save(dest_path, format="JPEG", quality=90, optimize=True)
        logger.debug("Saved processed image → %s", dest_path)
        return True
    except Exception as exc:
        logger.warning("Image processing failed for %s: %s", dest_path.name, exc)
        return False


# ── Internal helpers ──────────────────────────────────────────────────────────

def _center_crop(img: Image.Image) -> Image.Image:
    """
    Crop the image to a centered square matching its shorter dimension.

    E.g. a 640×480 image becomes 480×480 centered on the middle.
    This avoids aspect-ratio distortion when resizing to a fixed square.
    """
    w, h = img.size
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    return img.crop((left, top, left + side, top + side))
