# ─────────────────────────────────────────────────────────────────────────────
# Dockerfile – Smart Kitchen Image Collection Service
# ─────────────────────────────────────────────────────────────────────────────
# Multi-stage build is not used here because we need both Python runtime and
# Chrome browser in the final image.  The image is intentionally based on
# Debian (via python:3.11-slim) because Chromium packages require apt.
# ─────────────────────────────────────────────────────────────────────────────

FROM python:3.11-slim

# ── System dependencies ───────────────────────────────────────────────────────
RUN apt-get update && apt-get install -y --no-install-recommends \
        wget \
        gnupg \
        ca-certificates \
        fonts-liberation \
        libappindicator3-1 \
        libasound2 \
        libatk-bridge2.0-0 \
        libatk1.0-0 \
        libcups2 \
        libdbus-1-3 \
        libgdk-pixbuf2.0-0 \
        libnspr4 \
        libnss3 \
        libx11-xcb1 \
        libxcomposite1 \
        libxdamage1 \
        libxrandr2 \
        xdg-utils \
        # Google Chrome stable
        && wget -q -O /tmp/google-chrome.deb \
            https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb \
        && apt-get install -y /tmp/google-chrome.deb \
        && rm /tmp/google-chrome.deb \
        && apt-get clean \
        && rm -rf /var/lib/apt/lists/*

# ── Python dependencies ───────────────────────────────────────────────────────
WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# ── Application code ──────────────────────────────────────────────────────────
COPY app/ app/

# ── Persistent volume mount points ───────────────────────────────────────────
# Mount your host dataset/ directory here so images survive container restarts.
VOLUME ["/app/dataset"]

# ── Runtime environment ───────────────────────────────────────────────────────
ENV SELENIUM_HEADLESS=true \
    PORT=8000 \
    LOG_LEVEL=INFO \
    PYTHONUNBUFFERED=1

EXPOSE 8000

# ── Entry-point ───────────────────────────────────────────────────────────────
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
