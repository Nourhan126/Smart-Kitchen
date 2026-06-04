"""
main.py
-------
FastAPI application entry-point for the Smart Kitchen image collection service.

Endpoints
─────────
  GET  /health              – liveness probe
  POST /search-image        – single recipe image search & download
  POST /bulk-search         – upload CSV, process all recipes in background
  POST /bulk-search-local   – process recipes from local RAW_recipes after cleaning.csv

The server is intentionally stateless between requests: all persistent
state lives in the progress tracker (progress.json) and the images/ folder.
"""

import asyncio
import logging
import os
from contextlib import asynccontextmanager
from typing import Optional

import uvicorn
from fastapi import BackgroundTasks, FastAPI, File, HTTPException, UploadFile, status
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, JSONResponse

from app.config import IMAGES_DIR, INPUT_CSV, LOG_LEVEL, OUTPUT_CSV
from app.downloader import close_session
from app.models import BulkSearchStatus, HealthResponse, SingleSearchRequest, SingleSearchResponse
from app.pipeline import (
    bulk_process,
    get_tracker,
    parse_recipe_names_from_csv,
    search_and_download,
)
from app.progress import ProgressTracker

# ── Logging ───────────────────────────────────────────────────────────────────
logging.basicConfig(
    level=getattr(logging, LOG_LEVEL.upper(), logging.INFO),
    format="%(asctime)s [%(levelname)s] %(name)s – %(message)s",
)
logger = logging.getLogger(__name__)


# ── Application lifespan ──────────────────────────────────────────────────────

@asynccontextmanager
async def lifespan(app: FastAPI):
    """Start-up and shut-down hooks."""
    logger.info("Smart Kitchen Image Service starting up…")
    yield
    logger.info("Shutting down – closing HTTP session…")
    await close_session()
    logger.info("Shutdown complete.")


# ── FastAPI app ───────────────────────────────────────────────────────────────

app = FastAPI(
    title="Smart Kitchen Image Collection Service",
    description=(
        "Production-grade microservice that searches Google Images for recipe "
        "photos, validates quality, and serves results to the .NET backend."
    ),
    version="1.0.0",
    lifespan=lifespan,
)

# Allow .NET backend (and Swagger UI) to call the service from any origin
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# ── Background bulk-job state ─────────────────────────────────────────────────
# Simple in-process state for the running bulk job (one job at a time).
_bulk_job_lock = asyncio.Lock()
_bulk_job_running = False
_bulk_job_total = 0
_bulk_job_processed = 0
_bulk_job_success = 0
_bulk_job_failed = 0


# ── Endpoints ─────────────────────────────────────────────────────────────────

@app.get(
    "/health",
    response_model=HealthResponse,
    summary="Liveness probe",
    tags=["System"],
)
async def health() -> HealthResponse:
    """Return service status. Used by .NET health checks and load balancers."""
    return HealthResponse(status="running")


@app.post(
    "/search-image",
    response_model=SingleSearchResponse,
    summary="Search and download image for a single recipe",
    tags=["Images"],
)
async def search_image(request: SingleSearchRequest) -> SingleSearchResponse:
    """
    Search Google Images for the given recipe name, validate the best result,
    process it (resize/crop to 256×256), and return the local path.

    If the image was already downloaded in a previous call it is returned
    immediately without re-crawling.
    """
    logger.info(
        "Single search request: '%s' (%s)",
        request.recipe_name,
        request.target_type,
    )
    result = await search_and_download(
        request.recipe_name,
        request.target_type,
        request.context,
    )
    if not result.success:
        # Return 200 with success=false so the .NET client can handle it
        # gracefully rather than triggering exception handling.
        logger.warning("Search failed for '%s': %s", request.recipe_name, result.error)
    return result


@app.post(
    "/bulk-search",
    response_model=BulkSearchStatus,
    summary="Upload a CSV and process all recipes in the background",
    tags=["Images"],
)
async def bulk_search(
    background_tasks: BackgroundTasks,
    file: UploadFile = File(..., description="CSV file with a 'name' column"),
) -> BulkSearchStatus:
    """
    Accept a CSV upload and launch background processing for all recipes.

    - Already-downloaded recipes are skipped automatically.
    - Progress is persisted to disk so the job can be resumed after restart.
    - Poll `GET /bulk-status` to track progress.
    """
    global _bulk_job_running

    if not file.filename or not file.filename.lower().endswith(".csv"):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Please upload a .csv file.",
        )

    contents = await file.read()
    recipe_names = parse_recipe_names_from_csv(contents)

    if not recipe_names:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="No recipe names found in the CSV file.",
        )

    tracker = get_tracker()
    already_done = sum(1 for n in recipe_names if tracker.is_done(n))
    queued = len(recipe_names) - already_done

    if _bulk_job_running:
        return BulkSearchStatus(
            total_recipes=len(recipe_names),
            already_done=already_done,
            queued=queued,
            message="A bulk job is already running. Please wait for it to finish.",
        )

    # Schedule background task
    background_tasks.add_task(_run_bulk_job, recipe_names)

    return BulkSearchStatus(
        total_recipes=len(recipe_names),
        already_done=already_done,
        queued=queued,
        message=f"Bulk job started. Processing {queued} recipes in the background.",
    )


@app.post(
    "/bulk-search-local",
    response_model=BulkSearchStatus,
    summary="Process all recipes from the local RAW_recipes after cleaning.csv file",
    tags=["Images"],
)
async def bulk_search_local(background_tasks: BackgroundTasks) -> BulkSearchStatus:
    """
    Read recipes directly from the pre-placed CSV file at
    ``dataset/metadata/RAW_recipes after cleaning.csv`` and launch background
    processing for all recipes.

    - No file upload needed – the CSV must exist on disk before calling this endpoint.
    - Already-downloaded recipes are skipped automatically.
    - Poll ``GET /bulk-status`` to track progress.
    """
    global _bulk_job_running

    if not INPUT_CSV.exists():
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"Input CSV not found at '{INPUT_CSV}'. "
                   "Place 'RAW_recipes after cleaning.csv' in dataset/metadata/ first.",
        )

    recipe_names = parse_recipe_names_from_csv(INPUT_CSV.read_bytes())

    if not recipe_names:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail="No recipe names found in the CSV file.",
        )

    tracker = get_tracker()
    already_done = sum(1 for n in recipe_names if tracker.is_done(n))
    queued = len(recipe_names) - already_done

    if _bulk_job_running:
        return BulkSearchStatus(
            total_recipes=len(recipe_names),
            already_done=already_done,
            queued=queued,
            message="A bulk job is already running. Please wait for it to finish.",
        )

    background_tasks.add_task(_run_bulk_job, recipe_names)

    return BulkSearchStatus(
        total_recipes=len(recipe_names),
        already_done=already_done,
        queued=queued,
        message=f"Bulk job started from local CSV. Processing {queued} recipes in the background.",
    )


@app.get(
    "/bulk-status",
    summary="Check progress of the running bulk job",
    tags=["Images"],
)
async def bulk_status() -> JSONResponse:
    """Return current bulk-job counters and overall progress tracker stats."""
    tracker = get_tracker()
    return JSONResponse({
        "job_running": _bulk_job_running,
        "job_total": _bulk_job_total,
        "job_processed": _bulk_job_processed,
        "job_success": _bulk_job_success,
        "job_failed": _bulk_job_failed,
        "all_time_completed": tracker.completed_count,
        "all_time_failed": tracker.failed_count,
    })


@app.get(
    "/images/{file_path:path}",
    summary="Serve a downloaded image by filename",
    tags=["Images"],
)
async def serve_image(file_path: str) -> FileResponse:
    """
    Serve a downloaded image from the local dataset/images/ directory.
    Used by the .NET backend to display images without an extra storage layer.
    """
    path = IMAGES_DIR / file_path
    if not path.exists() or not path.is_file():
        raise HTTPException(status_code=404, detail="Image not found.")
    return FileResponse(str(path), media_type="image/jpeg")


# ── Background job helper ─────────────────────────────────────────────────────

async def _run_bulk_job(recipe_names: list) -> None:
    """Coroutine executed in the background by FastAPI's BackgroundTasks."""
    global _bulk_job_running, _bulk_job_total
    global _bulk_job_processed, _bulk_job_success, _bulk_job_failed

    async with _bulk_job_lock:
        _bulk_job_running = True
        _bulk_job_total = len(recipe_names)
        _bulk_job_processed = 0
        _bulk_job_success = 0
        _bulk_job_failed = 0

    try:
        async for idx, total, result in bulk_process(recipe_names):
            _bulk_job_processed = idx
            _bulk_job_total = total
            if result.success:
                _bulk_job_success += 1
            else:
                _bulk_job_failed += 1

        logger.info(
            "Bulk job complete: %d/%d succeeded",
            _bulk_job_success,
            _bulk_job_total,
        )
    except Exception as exc:
        logger.exception("Bulk job crashed: %s", exc)
    finally:
        _bulk_job_running = False


# ── Entry-point ───────────────────────────────────────────────────────────────

if __name__ == "__main__":
    port = int(os.getenv("PORT", "8000"))
    uvicorn.run("app.main:app", host="0.0.0.0", port=port, reload=False)
