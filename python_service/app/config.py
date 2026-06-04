"""
config.py
---------
Central configuration for the Smart Kitchen image collection service.
All tuneable knobs are gathered here so they can be overridden via
environment variables without touching code.
"""

import os
from pathlib import Path

# ── Base paths ────────────────────────────────────────────────────────────────

BASE_DIR = Path(__file__).resolve().parent.parent

# python_service/

DATASET_DIR = BASE_DIR / "dataset"

IMAGES_DIR = DATASET_DIR / "images"

METADATA_DIR = DATASET_DIR / "metadata"

PROGRESS_FILE = METADATA_DIR / "progress.json"

INPUT_CSV = METADATA_DIR / "RAW_recipes after cleaning.csv"

OUTPUT_CSV = METADATA_DIR / "output.csv"

# Ensure directories exist at import time

IMAGES_DIR.mkdir(
    parents=True,
    exist_ok=True
)

METADATA_DIR.mkdir(
    parents=True,
    exist_ok=True
)

# ── Image settings ────────────────────────────────────────────────────────────

IMAGE_SIZE = (
    256,
    256
)

# final output size (width, height)

MIN_IMAGE_DIM = 150

# pixels – skip images smaller than this

MAX_IMAGE_BYTES = 10 * 1024 * 1024

# 10 MB – skip oversized files

# ── Search / crawl settings ──────────────────────────────────────────────────

SEARCH_QUERY_SUFFIX = "food recipe dish"

MAX_RESULTS_TO_TRY = 8

# how many candidate images to attempt per recipe

SELENIUM_TIMEOUT = 15

# seconds to wait for page elements

SELENIUM_HEADLESS = os.getenv(
    "SELENIUM_HEADLESS",
    "true"
).lower() == "true"

CHROME_DRIVER_PATH = os.getenv(
    "CHROME_DRIVER_PATH",
    ""
)

# empty → auto-detect

# ── HTTP / download settings ─────────────────────────────────────────────────

HTTP_TIMEOUT = 20

# seconds for a single image download

MAX_RETRIES = 3

BACKOFF_BASE = 2.0

# seconds – multiplied by 2^attempt

CONNECTION_POOL_SIZE = 20

# aiohttp connector limit

# ── Bulk processing settings ─────────────────────────────────────────────────

BATCH_SIZE = int(
    os.getenv(
        "BATCH_SIZE",
        "10"
    )
)

# recipes processed per batch

MAX_WORKERS = int(
    os.getenv(
        "MAX_WORKERS",
        "4"
    )
)

# concurrent crawler threads

# ── Logging ───────────────────────────────────────────────────────────────────

LOG_LEVEL = os.getenv(
    "LOG_LEVEL",
    "INFO"
)
