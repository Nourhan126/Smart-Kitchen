using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartKitchen.API.Data;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Options;

namespace SmartKitchen.API.Services;

public interface IDatasetIngestionService
{
    Task<DatasetIngestionResult> SeedRecipesAsync(
        CancellationToken ct = default);

    Task<DatasetIngestionResult> SyncImagesAsync(
        CancellationToken ct = default);
}

public class DatasetIngestionService : IDatasetIngestionService
{
    private readonly ApplicationDbContext _db;
    private readonly DataSeeder _seeder;
    private readonly DatasetOptions _options;
    private readonly IImageUrlBuilder _imageUrlBuilder;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DatasetIngestionService> _logger;

    public DatasetIngestionService(
        ApplicationDbContext db,
        DataSeeder seeder,
        IOptions<DatasetOptions> options,
        IImageUrlBuilder imageUrlBuilder,
        IWebHostEnvironment env,
        ILogger<DatasetIngestionService> logger)
    {
        _db = db;
        _seeder = seeder;
        _options = options.Value;
        _imageUrlBuilder = imageUrlBuilder;
        _env = env;
        _logger = logger;
    }

    public async Task<DatasetIngestionResult> SeedRecipesAsync(
        CancellationToken ct = default)
    {
        var result = new DatasetIngestionResult();

        var recipeCsv = ResolvePath(
            _options.RecipeCsvPath,
            "python_service/dataset/metadata/RAW_recipes after cleaning.csv");

        if (!File.Exists(recipeCsv))
        {
            _logger.LogWarning(
                "Recipe CSV not found at {Path}",
                recipeCsv);

            return result;
        }

        var beforeCount = await _db.Recipes.CountAsync(ct);

        await _seeder.SeedAsync(recipeCsv);

        var afterCount = await _db.Recipes.CountAsync(ct);

        result.RecipesSeeded =
            Math.Max(0, afterCount - beforeCount);

        return result;
    }

    public async Task<DatasetIngestionResult> SyncImagesAsync(
        CancellationToken ct = default)
    {
        var result = new DatasetIngestionResult();

        var imageCsv = ResolvePath(
            _options.ImageMetadataCsvPath,
            "python_service/dataset/metadata/final_image_dataset.csv");

        var progressJson = ResolvePath(
            _options.ProgressJsonPath,
            "python_service/dataset/metadata/progress.json");

        var imagesRoot = ResolvePath(
            _options.ImagesRootPath,
            "python_service/dataset/images");

        var entries = new List<(string Name, string FilePath)>();

        if (File.Exists(imageCsv))
        {
            entries.AddRange(
                await LoadImageMetadataAsync(imageCsv, ct));
        }
        else if (File.Exists(progressJson))
        {
            entries.AddRange(
                await LoadProgressJsonAsync(progressJson, ct));
        }

        if (entries.Count == 0)
        {
            _logger.LogWarning(
                "No image metadata available to sync.");

            return result;
        }

        var recipes = await _db.Recipes
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.ImageUrl
            })
            .ToListAsync(ct);

        var nameLookup = recipes
            .GroupBy(
                r => r.Name.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().Id,
                StringComparer.OrdinalIgnoreCase);

        var updated = 0;
        var batch = 0;

        foreach (var entry in entries)
        {
            if (!nameLookup.TryGetValue(
                    entry.Name.Trim(),
                    out var recipeId))
            {
                continue;
            }

            var sourcePath = entry.FilePath;

            if (!Path.IsPathRooted(sourcePath))
            {
                sourcePath = Path.Combine(
                    _env.ContentRootPath,
                    sourcePath);
            }

            if (!File.Exists(sourcePath))
            {
                var fallback = Path.Combine(
                    imagesRoot,
                    Path.GetFileName(entry.FilePath));

                if (File.Exists(fallback))
                {
                    sourcePath = fallback;
                }
                else
                {
                    continue;
                }
            }

            var imageUrl =
                _imageUrlBuilder.BuildPublicImageUrl(
                    new ImageSearchResponse(
                        entry.Name,
                        "recipe",
                        sourcePath,
                        null,
                        true,
                        null));

            var recipe = new Models.Recipe
            {
                Id = recipeId,
                ImageUrl = imageUrl
            };

            _db.Recipes.Attach(recipe);

            _db.Entry(recipe)
                .Property(r => r.ImageUrl)
                .IsModified = true;

            updated++;
            batch++;

            if (batch >= 200)
            {
                await _db.SaveChangesAsync(ct);
                batch = 0;
            }
        }

        if (batch > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        result.ImagesSynced = updated;

        return result;
    }

    private async Task<List<(string Name, string FilePath)>>
        LoadImageMetadataAsync(
            string path,
            CancellationToken ct)
    {
        var entries =
            new List<(string Name, string FilePath)>();

        using var stream = File.OpenRead(path);

        using var reader = new StreamReader(stream);

        var config = new CsvConfiguration(
            CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();

        csv.ReadHeader();

        var headers =
            csv.HeaderRecord ?? Array.Empty<string>();

        var nameColumn = headers.FirstOrDefault(h =>
            string.Equals(
                h,
                "recipe_name",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                h,
                "name",
                StringComparison.OrdinalIgnoreCase))
            ?? headers.FirstOrDefault();

        var pathColumn = headers.FirstOrDefault(h =>
            string.Equals(
                h,
                "image_path",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                h,
                "local_image_name",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                h,
                "image",
                StringComparison.OrdinalIgnoreCase));

        if (nameColumn == null || pathColumn == null)
        {
            return entries;
        }

        while (await csv.ReadAsync())
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var name =
                csv.GetField(nameColumn)?.Trim();

            var imagePath =
                csv.GetField(pathColumn)?.Trim();

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(imagePath))
            {
                continue;
            }

            entries.Add((name, imagePath));
        }

        return entries;
    }

    private async Task<List<(string Name, string FilePath)>>
        LoadProgressJsonAsync(
            string path,
            CancellationToken ct)
    {
        var entries =
            new List<(string Name, string FilePath)>();

        var json =
            await File.ReadAllTextAsync(path, ct);

        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty(
                "completed",
                out var completed))
        {
            return entries;
        }

        foreach (var item in completed.EnumerateObject())
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var node = item.Value;

            if (!node.TryGetProperty(
                    "recipe_name",
                    out var recipeName))
            {
                continue;
            }

            var name = recipeName.GetString();

            var imagePath =
                node.TryGetProperty(
                    "image_path",
                    out var imagePathProp)
                ? imagePathProp.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(name) &&
                !string.IsNullOrWhiteSpace(imagePath))
            {
                entries.Add((name!, imagePath!));
            }
        }

        return entries;
    }

    private string ResolvePath(
        string? configured,
        string fallback)
    {
        var path = string.IsNullOrWhiteSpace(configured)
            ? fallback
            : configured;

        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(
                _env.ContentRootPath,
                path);
    }
}
