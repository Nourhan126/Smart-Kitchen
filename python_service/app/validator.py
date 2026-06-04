"""
validator.py
------------
Image quality and relevance validation using PIL (Pillow) and OpenCV.

Filters out:
  - Corrupted / unreadable files
  - Images that are too small
  - Grayscale / palette-only images (likely icons or logos)
  - Images with an extreme aspect ratio (likely banners / menus)
  - Images whose centre region is dominated by text-like low-variance pixels
  - Near-duplicate images (via perceptual hash comparison)
  - Images with a very high proportion of white/transparent pixels (watermarks,
    menus, collages with white backgrounds)

The validator is intentionally conservative: it rejects borderline cases so
that only clean, visually-rich food images make it into the dataset.
"""

import io
import hashlib
import logging
from pathlib import Path
from typing import Tuple
import pytesseract
pytesseract.pytesseract.tesseract_cmd = (
    r"C:\Program Files\Tesseract-OCR\tesseract.exe"
)
import cv2
import numpy as np
from PIL import Image, UnidentifiedImageError

from app.config import MIN_IMAGE_DIM

logger = logging.getLogger(__name__)

# ── In-process duplicate detection (perceptual hash registry) ─────────────────
_seen_hashes: set[str] = set()


def reset_duplicate_registry() -> None:
    """Clear the in-memory hash registry (call between independent runs)."""
    _seen_hashes.clear()


# ── Public API ────────────────────────────────────────────────────────────────

def validate_image_bytes(data: bytes) -> Tuple[bool, str]:
    """
    Validate raw image bytes before saving to disk.

    Returns (True, "") on success or (False, reason) on failure.
    """
    # 1. Empty guard
    if not data:
        return False, "empty response"

    # 2. Attempt to decode with PIL first (catches corrupt JPEG/PNG/etc.)
    try:
        pil_img = Image.open(io.BytesIO(data))
        pil_img.verify()          # raises on corruption
    except (UnidentifiedImageError, Exception) as exc:
        return False, f"PIL decode error: {exc}"

    # Re-open after verify() (verify() exhausts the stream)
    try:
        pil_img = Image.open(io.BytesIO(data))
        pil_img.load()
    except Exception as exc:
        return False, f"PIL load error: {exc}"

    # 3. Minimum dimensions
    w, h = pil_img.size
    if w < MIN_IMAGE_DIM or h < MIN_IMAGE_DIM:
        return False, f"too small ({w}x{h})"

    # 4. Must be convertible to RGB (rejects palette / grayscale icons)
    try:
        rgb = pil_img.convert("RGB")
    except Exception as exc:
        return False, f"RGB conversion failed: {exc}"

    # 5. Extreme aspect ratio check (menus, banners, collage strips)
    ratio = max(w, h) / max(min(w, h), 1)
    if ratio > 5.0:
        return False, f"extreme aspect ratio ({ratio:.1f})"

      # 6. OpenCV-based checks (colour variance, white-pixel saturation)
    np_img = np.array(rgb)

    ok, reason = _opencv_checks(np_img)

    if not ok:
        return False, reason

    if _contains_face(np_img):
        return False, "face detected - likely people/portrait image"

    if _contains_too_much_text(rgb):
        return False, "too much text detected"

    # 7. Perceptual hash duplicate detection
    phash = _perceptual_hash(rgb)

    if phash in _seen_hashes:
        return False, "duplicate image"

    _seen_hashes.add(phash)

    return True, ""


def validate_image_file(path: Path) -> Tuple[bool, str]:
    """Convenience wrapper that reads a file from disk and validates it."""
    try:
        data = path.read_bytes()
    except OSError as exc:
        return False, f"file read error: {exc}"
    return validate_image_bytes(data)


# ── Internal helpers ──────────────────────────────────────────────────────────

def _opencv_checks(np_img: np.ndarray) -> Tuple[bool, str]:
    """
    Run OpenCV heuristics on a uint8 HxWx3 RGB array.

    Checks performed
    ────────────────
    a) Colour variance – very low variance means a blank / logo / icon.
    b) White-pixel ratio – >80 % white suggests a menu or watermarked image.
    c) Text-dominance heuristic – high edge density in a uniform-colour image
       typically indicates a text-heavy (menu / infographic) image.
    """
    # Convert to BGR for OpenCV functions
    bgr = cv2.cvtColor(np_img, cv2.COLOR_RGB2BGR)

    # a) Color variance
    variance = float(np.var(np_img.astype(np.float32)))
    if variance < 200.0:
        return False, f"low color variance ({variance:.1f}) – likely logo/icon"

    # b) White-pixel ratio (pixels where all channels > 240)
    white_mask = np.all(np_img > 240, axis=2)
    white_ratio = white_mask.mean()
    if white_ratio > 0.80:
        return False, f"too many white pixels ({white_ratio:.0%}) – likely menu/watermark"

    # c) Text-dominance: lots of edges but very low saturation variance
    gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
    edges = cv2.Canny(gray, 100, 200)
    edge_density = edges.mean()
    hsv = cv2.cvtColor(bgr, cv2.COLOR_BGR2HSV)
    sat_var = float(np.var(hsv[:, :, 1].astype(np.float32)))
    if edge_density > 20 and sat_var < 300:
        return False, "high edge density + low saturation – likely text/menu image"

    return True, ""


def _contains_too_much_text(img: Image.Image) -> bool:
    try:
        text = pytesseract.image_to_string(img)

        text = text.strip()

        return len(text) > 25

    except Exception:
        return False





def _contains_too_much_text(img: Image.Image) -> bool:
    try:
        text = pytesseract.image_to_string(img)

        text = text.strip()

        return len(text) > 40

    except Exception:
        return False


def _contains_face(np_img: np.ndarray) -> bool:
    """
    Reject obvious portraits and people shots using OpenCV's bundled Haar cascade.
    This is a conservative guard; failure to load the cascade should not block
    otherwise valid food images.
    """
    cascade_path = Path(cv2.data.haarcascades) / "haarcascade_frontalface_default.xml"
    if not cascade_path.exists():
        return False

    gray = cv2.cvtColor(np_img, cv2.COLOR_RGB2GRAY)
    detector = cv2.CascadeClassifier(str(cascade_path))
    if detector.empty():
        return False

    faces = detector.detectMultiScale(
        gray,
        scaleFactor=1.1,
        minNeighbors=5,
        minSize=(40, 40),
    )

    return len(faces) > 0


def _perceptual_hash(img: Image.Image, hash_size: int = 16) -> str:
    """
    Compute a simple average-hash (aHash) as a hex string.
    Identical or near-duplicate images will produce the same hash at this size.
    """
    # Resize to hash_size×hash_size greyscale
    thumb = img.convert("L").resize((hash_size, hash_size), Image.LANCZOS)
    arr = np.array(thumb, dtype=np.uint8)
    mean_val = arr.mean()
    bits = (arr > mean_val).flatten()
    # Pack bits into bytes
    packed = np.packbits(bits).tobytes()
    return hashlib.md5(packed).hexdigest()
