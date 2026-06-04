"""
crawler.py
-----------
DDGS-based image crawler.

Improved filtering:
- Removes menus
- Removes recipe cards
- Removes infographics
- Removes PDFs/documents
- Removes software screenshots
- Removes advertisements
- Removes product packaging images
- Prefers results whose title matches the recipe name
"""

import logging
import re
from typing import List

from ddgs import DDGS

from app.config import MAX_RESULTS_TO_TRY

logger = logging.getLogger(__name__)


BLOCKED_URL_TERMS = {
    "logo",
    "avatar",
    "profile",
    "portrait",
    "headshot",
    "people",
    "person",
    "banner",
    "advert",
    "advertisement",
    "menu",
    "infographic",
    "icon",
    "pdf",
    "document",
    "worksheet",
    "template",
    "guide",
}


BLOCKED_TITLE_TERMS = {
    "stock photo",
"getty images",
"shutterstock",
"istock",
"vector",
"clipart",
"cartoon",
"drawing",
"illustration",
"mockup",
"restaurant menu",
"nutrition",
"calories",
"calorie",
"facts",
    "recipe card",
    "nutrition facts",
    "nutrition label",
    "pdf",
    "document",
    "worksheet",
    "template",
    "menu",
    "infographic",
    "guide",
    "ebook",
    "poster",
    "advertisement",
    "ad",
    "coupon",
    "flyer",
    "software",
    "application",
    "dashboard",
    "screenshot",
    "screen shot",
    "computer screen",
    "packaging",
    "food package",
    "label",
}


STOP_WORDS = {
    "recipe",
    "food",
    "dish",
    "easy",
    "best",
    "homemade",
    "minute",
    "minutes",
    "quick",
    "and",
    "with",
    "for",
    "the",
    "a",
    "an",
}


def _normalize_words(text: str) -> set:
    words = re.findall(r"\w+", text.lower())

    return {
        w
        for w in words
        if len(w) > 2 and w not in STOP_WORDS
    }


def _recipe_matches_title(
    recipe_name: str,
    title: str,
) -> bool:
    """
    Require a reasonable overlap between recipe name
    and DDGS result title.
    """

    recipe_words = _normalize_words(recipe_name)
    title_words = _normalize_words(title)

    if not recipe_words:
        return True

    common = recipe_words.intersection(title_words)

    score = len(common) / len(recipe_words)

    logger.debug(
        "Recipe match score %.2f | recipe=%s | title=%s",
        score,
        recipe_name,
        title,
    )

    return score >= 0.60


def _is_valid_result(
    result: dict,
    recipe_name: str,
) -> bool:
    """
    Check DDGS result metadata before accepting image.
    """

    image_url = result.get("image")

    if not image_url:
        return False

    if not image_url.startswith("http"):
        return False

    lowered_url = image_url.lower()

    if any(term in lowered_url for term in BLOCKED_URL_TERMS):
        return False

    title = (result.get("title") or "").lower()

    if any(term in title for term in BLOCKED_TITLE_TERMS):
        return False

    if title:

        if not _recipe_matches_title(
            recipe_name,
            title,
        ):
            return False

    source = (result.get("source") or "").lower()

    if any(term in source for term in (
        "pinterest",
        "facebook",
        "twitter",
        "x.com",
    )):
        return False

    width = result.get("width")
    height = result.get("height")

    try:

        if width and height:

            width = int(width)
            height = int(height)

            if width < 200 or height < 200:
                return False

    except Exception:
        pass

    return True


def search_google_images(recipe_name: str) -> List[str]:
    """
    Search for recipe food images using DDGS.

    Returns:
        List[str]: candidate image URLs
    """

    query = (
    f'"{recipe_name}" '
    f'finished dish recipe food photography plated'
)

    logger.info(
        f"Searching images for: {query}"
    )

    try:

        with DDGS() as ddgs:

            results = list(
                ddgs.images(
                    query,
                    max_results=MAX_RESULTS_TO_TRY * 5
                )
            )

        urls = []

        for result in results:

            if not _is_valid_result(
                result,
                recipe_name,
            ):
                continue

            image_url = result["image"]

            urls.append(image_url)

        urls = list(
            dict.fromkeys(urls)
        )

        logger.info(
            f"Found {len(urls)} filtered image URLs for {recipe_name}"
        )

        return urls[:MAX_RESULTS_TO_TRY]

    except Exception as e:

        logger.error(
            f"DDGS image search failed for {recipe_name}: {e}"
        )

        return []