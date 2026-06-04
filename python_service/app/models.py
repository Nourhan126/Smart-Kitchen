"""
models.py
---------
Pydantic request/response schemas used by the FastAPI endpoints.
Keeping them in a dedicated module makes it easy to version the API
contract independently of business logic.
"""

from typing import Optional
from pydantic import BaseModel, Field


# ── Request schemas ───────────────────────────────────────────────────────────

class SingleSearchRequest(BaseModel):
    """Body for POST /search-image."""
    recipe_name: str = Field(
        ...,
        min_length=1,
        max_length=500,
        json_schema_extra={"example": "Pesto Pizza"},
    )
    target_type: str = Field(
        "recipe",
        json_schema_extra={"example": "ingredient"},
    )
    context: Optional[str] = Field(
        None,
        max_length=500,
        json_schema_extra={"example": "Step 1 mixing oats and chia seeds"},
    )


# ── Response schemas ──────────────────────────────────────────────────────────

class SingleSearchResponse(BaseModel):
    """Response for POST /search-image."""
    recipe_name: str
    target_type: str = "recipe"
    image_path: Optional[str] = None   # local filesystem path
    image_url: Optional[str] = None    # original source URL
    success: bool
    error: Optional[str] = None        # populated when success=False


class BulkSearchStatus(BaseModel):
    """Response for POST /bulk-search."""
    total_recipes: int
    already_done: int
    queued: int
    message: str


class HealthResponse(BaseModel):
    """Response for GET /health."""
    status: str = "running"
