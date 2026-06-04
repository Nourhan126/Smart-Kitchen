# Smart Kitchen – Image Collection Microservice

A production-grade Python microservice that **automatically searches Google Images**, validates quality, and downloads food photos for 194,000+ recipes. It exposes a REST API consumed by the ASP.NET Core backend.

---

## Architecture

```
python_service/
├── app/
│   ├── main.py          ← FastAPI server (3 endpoints)
│   ├── config.py        ← All configuration / env vars
│   ├── models.py        ← Pydantic request/response schemas
│   ├── crawler.py       ← Selenium + Google Images crawler
│   ├── downloader.py    ← async aiohttp downloader + retry
│   ├── validator.py     ← PIL + OpenCV image quality checks
│   ├── processor.py     ← center-crop + 256×256 JPEG output
│   ├── pipeline.py      ← single & bulk orchestration
│   └── progress.py      ← resumable progress tracker (JSON)
├── dataset/
│   ├── images/          ← downloaded & processed images
│   └── metadata/
│       ├── progress.json           ← auto-saved job state
│       └── generated output
├── dotnet_integration/
│   ├── ImageSearchService.cs  ← typed HttpClient service
│   ├── ImageController.cs     ← ASP.NET Core API controller
│   └── ProgramExtensions.cs   ← DI registration + Polly policies
├── tests/               ← pytest unit tests
├── requirements.txt
├── Dockerfile
└── docker-compose.yml
```

---

## Quick Start

### Option A – Docker (recommended)

```bash
cd python_service
docker compose up --build
```

The API is available at **http://localhost:8000**.  
Interactive docs: **http://localhost:8000/docs**

---

### Option B – Local virtual environment

**Prerequisites:** Python 3.11+, Google Chrome installed.

```bash
cd python_service
python -m venv .venv
source .venv/bin/activate          # Windows: .venv\Scripts\activate
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8000
```

---

## API Reference

### `GET /health`
Liveness probe.
```json
{ "status": "running" }
```

---

### `POST /search-image`
Find, validate, and download a food image for one recipe.

**Request**
```json
{ "recipe_name": "Pesto Pizza" }
```

**Response**
```json
{
  "recipe_name": "Pesto Pizza",
  "image_path": "dataset/images/pesto_pizza.jpg",
  "image_url": "https://cdn.example.com/pesto-pizza.jpg",
  "success": true
}
```

---

### `POST /bulk-search`
Upload a CSV file; processing runs in the background.

```bash
curl -X POST http://localhost:8000/bulk-search \
     -F "file=@recipes.csv"
```

**CSV format** – any of these column names are auto-detected:  
`name` · `recipe_name` · `title` · `recipe`

**Response**
```json
{
  "total_recipes": 194000,
  "already_done": 12500,
  "queued": 181500,
  "message": "Bulk job started. Processing 181500 recipes in the background."
}
```

### `GET /bulk-status`
Poll this endpoint to watch bulk-job progress.

---

## Configuration

All settings can be overridden with environment variables:

| Variable | Default | Description |
|---|---|---|
| `SELENIUM_HEADLESS` | `true` | Run Chrome without a GUI |
| `CHROME_DRIVER_PATH` | *(auto)* | Path to chromedriver binary |
| `BATCH_SIZE` | `50` | Recipes per processing batch |
| `MAX_WORKERS` | `4` | Concurrent crawler threads |
| `LOG_LEVEL` | `INFO` | Python logging level |
| `PORT` | `8000` | HTTP port |

---

## .NET Integration

### 1 – Copy the integration files

Copy these three files from `dotnet_integration/` into your .NET project:

```
ImageSearchService.cs   → Services/
ImageController.cs      → Controllers/
ProgramExtensions.cs    → Extensions/
```

### 2 – Register the service in `Program.cs`

```csharp
// Add near the top of Program.cs, before builder.Build()
builder.Services.AddImageSearchService(builder.Configuration);
```

### 3 – Add the base URL to `appsettings.json`

```json
{
  "ImageService": {
    "BaseUrl": "http://localhost:8000"
  }
}
```

### 4 – Use it in a controller or service

```csharp
public class RecipesController : ControllerBase
{
    private readonly IImageSearchService _images;

    public RecipesController(IImageSearchService images) => _images = images;

    [HttpPost("{id}/fetch-image")]
    public async Task<IActionResult> FetchImage(int id)
    {
        var recipe = await _recipeService.GetByIdAsync(id);
        var result = await _images.SearchImageAsync(recipe.Name);
        if (result?.Success == true)
        {
            recipe.ImageUrl = result.ImageUrl;
            await _recipeService.UpdateAsync(recipe);
        }
        return Ok(result);
    }
}
```

---

## Running Tests

```bash
cd python_service
pip install -r requirements.txt pytest
pytest tests/ -v
```

---

## Storage Layout

After processing, your `dataset/` directory will look like:

```
dataset/
├── images/
│   ├── pesto_pizza.jpg
│   ├── chicken_alfredo_pasta.jpg
│   └── ...
└── metadata/
    ├── progress.json           ← resume checkpoint
    └── final output
```

---

## Resumable Processing

If the bulk job is interrupted (server restart, OOM, etc.):

1. The `progress.json` file records every completed recipe.
2. Re-calling `POST /bulk-search` with the same CSV automatically skips  
   all already-completed recipes and continues from where it left off.

---

## Image Validation Rules

Images are rejected if they are:

| Condition | Detection method |
|---|---|
| Corrupted / unreadable | PIL `verify()` + `load()` |
| Too small (< 150 px) | PIL dimensions check |
| Extreme aspect ratio (> 5:1) | PIL dimensions check |
| Very low colour variance (logos, icons) | OpenCV `np.var` |
| Mostly white pixels (menus, watermarks) | OpenCV white-pixel ratio |
| High-edge + low-saturation (text-heavy) | OpenCV Canny + HSV |
| Duplicate | Perceptual hash (aHash) |
