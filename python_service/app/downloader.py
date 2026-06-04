"""
downloader.py
-------------
Async image downloader with retry logic and exponential back-off.

Uses aiohttp with a shared connection pool (TCPConnector) to efficiently
download potentially hundreds of images concurrently while respecting
server rate limits.

Retry strategy
──────────────
  attempt 1 → immediate
  attempt 2 → wait BACKOFF_BASE seconds
  attempt 3 → wait BACKOFF_BASE * 2 seconds
  ... up to MAX_RETRIES

A 429 (Too Many Requests) response triggers a longer pause before retrying.
"""

import asyncio
import logging
from typing import Optional

import aiohttp

from app.config import (
    BACKOFF_BASE,
    CONNECTION_POOL_SIZE,
    HTTP_TIMEOUT,
    MAX_RETRIES,
)

logger = logging.getLogger(__name__)

# ── Shared connector (created once per process) ───────────────────────────────
_connector: Optional[aiohttp.TCPConnector] = None
_session: Optional[aiohttp.ClientSession] = None

_HEADERS = {
    "User-Agent": (
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
        "AppleWebKit/537.36 (KHTML, like Gecko) "
        "Chrome/124.0.0.0 Safari/537.36"
    ),
    "Accept": "image/webp,image/apng,image/*,*/*;q=0.8",
    "Accept-Language": "en-US,en;q=0.9",
}


async def get_session() -> aiohttp.ClientSession:
    """Return (or lazily create) the shared aiohttp session."""
    global _connector, _session
    if _session is None or _session.closed:
        _connector = aiohttp.TCPConnector(
            limit=CONNECTION_POOL_SIZE,
            ssl=False,          # skip SSL verification for image CDNs
            force_close=False,  # keep-alive
        )
        timeout = aiohttp.ClientTimeout(total=HTTP_TIMEOUT)
        _session = aiohttp.ClientSession(
            connector=_connector,
            headers=_HEADERS,
            timeout=timeout,
        )
    return _session


async def close_session() -> None:
    """Gracefully close the shared session (call on app shutdown)."""
    global _session, _connector
    if _session and not _session.closed:
        await _session.close()
    if _connector:
        await _connector.close()
    _session = None
    _connector = None


async def download_image(url: str) -> Optional[bytes]:
    """
    Download an image from *url* and return its raw bytes.

    Returns None if all retries are exhausted or the response is not an image.
    Implements exponential back-off between retries.
    """
    session = await get_session()

    for attempt in range(1, MAX_RETRIES + 1):
        try:
            async with session.get(url, allow_redirects=True) as resp:
                # Rate-limit handling
                if resp.status == 429:
                    wait = BACKOFF_BASE * (2 ** attempt)
                    logger.warning(
                        "Rate-limited downloading %s – waiting %.1fs (attempt %d/%d)",
                        url, wait, attempt, MAX_RETRIES,
                    )
                    await asyncio.sleep(wait)
                    continue

                if resp.status != 200:
                    logger.debug("HTTP %d for %s", resp.status, url)
                    return None

                # Reject non-image content types
                content_type = resp.headers.get("Content-Type", "")
                if not content_type.startswith("image/"):
                    logger.debug("Non-image content-type '%s' for %s", content_type, url)
                    return None

                data = await resp.read()
                return data

        except (aiohttp.ClientError, asyncio.TimeoutError) as exc:
            backoff = BACKOFF_BASE * (2 ** (attempt - 1))
            logger.warning(
                "Download attempt %d/%d failed for %s: %s – retrying in %.1fs",
                attempt, MAX_RETRIES, url, exc, backoff,
            )
            if attempt < MAX_RETRIES:
                await asyncio.sleep(backoff)

    logger.error("All %d download attempts failed for %s", MAX_RETRIES, url)
    return None
