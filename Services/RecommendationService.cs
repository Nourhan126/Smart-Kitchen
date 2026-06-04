using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartKitchen.API.Data;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Models;
using SmartKitchen.API.Options;

namespace SmartKitchen.API.Services;

public interface IRecommendationService
{
    Task<List<int>> GetRecommendedIdsAsync(
        string? userId,
        int count,
        CancellationToken ct = default);

    Task<List<int>> GetSeasonalIdsAsync(
        string? userId,
        int count,
        string? season = null,
        CancellationToken ct = default);

    Task<List<int>> GetRecommendationsForRecipeAsync(
        int recipeId,
        int count,
        CancellationToken ct = default);

    Task<RecommendationImportResult> ImportAsync(
        CancellationToken ct = default);
}

public class RecommendationService : IRecommendationService
{
    private readonly ApplicationDbContext _db;
    private readonly DatasetOptions _options;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<RecommendationService> _logger;

    private const string SourceGeneral = "dataset";
    private const string SourceSeasonal = "seasonal";

    public RecommendationService(
        ApplicationDbContext db,
        IOptions<DatasetOptions> options,
        IWebHostEnvironment env,
        ILogger<RecommendationService> logger)
    {
        _db = db;
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<List<int>> GetRecommendedIdsAsync(
        string? userId,
        int count,
        CancellationToken ct = default)
    {
        var ids =
            await _db.RecipeRecommendations
                .Where(r => r.Source == SourceGeneral)
                .OrderBy(r => r.Rank)
                .ThenByDescending(r => r.Score)
                .Select(r => r.RecommendedRecipeId)
                .Distinct()
                .Take(count)
                .ToListAsync(ct);

        return ids;
    }

    public async Task<List<int>> GetSeasonalIdsAsync(
        string? userId,
        int count,
        string? season = null,
        CancellationToken ct = default)
    {
        TimeZoneInfo egyptTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                "Egypt Standard Time");

        var egyptNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                egyptTimeZone);

        season ??= GetSeasonForDate(egyptNow);

        var ids =
            await _db.RecipeRecommendations
                .Where(r =>
                    r.Source == SourceSeasonal &&
                    r.Season == season)
                .OrderBy(r => r.Rank)
                .ThenByDescending(r => r.Score)
                .Select(r => r.RecommendedRecipeId)
                .Distinct()
                .Take(count)
                .ToListAsync(ct);

        return ids;
    }

    public async Task<List<int>> GetRecommendationsForRecipeAsync(
        int recipeId,
        int count,
        CancellationToken ct = default)
    {
        return await _db.RecipeRecommendations
            .Where(r =>
                r.Source == SourceGeneral &&
                r.RecipeId == recipeId)
            .OrderBy(r => r.Rank)
            .ThenByDescending(r => r.Score)
            .Select(r => r.RecommendedRecipeId)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<RecommendationImportResult> ImportAsync(
        CancellationToken ct = default)
    {
        var result =
            new RecommendationImportResult();

        var recipeIds =
            (await _db.Recipes
                .Select(r => r.Id)
                .ToListAsync(ct))
            .ToHashSet();

        var generalPath =
            ResolvePath(
                _options.RecommendationZipPath ??
                _options.RecommendationPath,
                "python_service/dataset/metadata/final_recommendation 2.zip");

        var seasonalPath =
            ResolvePath(
                _options.SeasonalRecommendationZipPath ??
                _options.SeasonalRecommendationPath,
                "python_service/dataset/metadata/recommendation_with_season.zip");

        if (File.Exists(generalPath))
        {
            var importResult =
                await ImportFromZipAsync(
                    generalPath,
                    SourceGeneral,
                    recipeIds,
                    ct);

            result.TotalImported +=
                importResult.Imported;

            result.TotalSkipped +=
                importResult.Skipped;

            result.TotalSources++;
        }

        if (File.Exists(seasonalPath))
        {
            var importResult =
                await ImportFromZipAsync(
                    seasonalPath,
                    SourceSeasonal,
                    recipeIds,
                    ct);

            result.TotalImported +=
                importResult.Imported;

            result.TotalSkipped +=
                importResult.Skipped;

            result.TotalSources++;
        }

        result.TotalSkipped =
            Math.Max(0, result.TotalSkipped);

        return result;
    }

    private async Task<(int Imported, int Skipped)>
        ImportFromZipAsync(
            string path,
            string source,
            HashSet<int> recipeIds,
            CancellationToken ct)
    {
        _logger.LogInformation(
            "Importing recommendations from {Path}",
            path);

        var imported = 0;
        var skipped = 0;

        await _db.RecipeRecommendations
            .Where(r => r.Source == source)
            .ExecuteDeleteAsync(ct);

        using var archive =
            ZipFile.OpenRead(path);

        var csvEntry =
            archive.Entries
                .FirstOrDefault(e =>
                    e.FullName.EndsWith(
                        ".csv",
                        StringComparison.OrdinalIgnoreCase));

        if (csvEntry == null)
        {
            _logger.LogWarning(
                "No CSV entries found in {Path}",
                path);

            return (imported, skipped);
        }

        await using var entryStream =
            csvEntry.Open();

        using var reader =
            new StreamReader(entryStream);

        var config =
            new CsvConfiguration(
                CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,

                MissingFieldFound = args =>
                    _logger.LogWarning(
                        "Missing field at row {Row} index {Index}: {Headers}",
                        args.Context?.Parser?.Row ?? 0,
                        args.Index,
                        string.Join(
                            ", ",
                            args.HeaderNames ?? [])),

                BadDataFound = args =>
                    _logger.LogWarning(
                        "Bad data at row {Row}: {Raw}",
                        args.Context?.Parser?.Row ?? 0,
                        args.RawRecord)
            };

        using var csv =
            new CsvReader(reader, config);

        await csv.ReadAsync();

        csv.ReadHeader();

        var headers =
            csv.HeaderRecord ??
            Array.Empty<string>();

        var headerLookup =
            headers
                .Select(h => h.Trim())
                .ToList();

        var recipeIdColumn =
            FindColumn(
                headerLookup,
                "recipe_id",
                "recipeid",
                "id",
                "recipe");

        var seasonColumn =
            FindColumn(
                headerLookup,
                "season",
                "season_name");

        var scoreColumn =
            FindColumn(
                headerLookup,
                "score",
                "similarity",
                "confidence");

        var recommendedIdColumn =
            FindColumn(
                headerLookup,
                "recommended_recipe_id",
                "recommended_id",
                "recommendation_id");

        var listColumns =
            headerLookup
                .Where(h =>
                    h.Contains(
                        "recommend",
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        var batch =
            new List<RecipeRecommendation>();

        while (await csv.ReadAsync())
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (!TryGetInt(
                    csv,
                    recipeIdColumn,
                    out var recipeId) ||
                !recipeIds.Contains(recipeId))
            {
                skipped++;
                continue;
            }

            var season =
                seasonColumn != null
                    ? csv.GetField(seasonColumn)
                    : null;

            var score =
                TryGetDouble(
                    csv,
                    scoreColumn,
                    out var s)
                        ? s
                        : 0;

            var recommendations =
                new List<int>();

            if (recommendedIdColumn != null &&
                TryGetInt(
                    csv,
                    recommendedIdColumn,
                    out var singleId))
            {
                recommendations.Add(singleId);
            }
            else
            {
                foreach (var column in listColumns)
                {
                    var raw =
                        csv.GetField(column);

                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    recommendations.AddRange(
                        ParseIdList(raw));
                }
            }

            var rank = 0;

            foreach (var recommendedId in recommendations.Distinct())
            {
                if (!recipeIds.Contains(recommendedId))
                {
                    skipped++;
                    continue;
                }

                rank++;

                batch.Add(
                    new RecipeRecommendation
                    {
                        RecipeId = recipeId,
                        RecommendedRecipeId = recommendedId,
                        Score = score,
                        Rank = rank,
                        Season =
                            string.IsNullOrWhiteSpace(season)
                                ? null
                                : season,
                        Source = source
                    });

                if (batch.Count >= 500)
                {
                    await _db.RecipeRecommendations
                        .AddRangeAsync(batch, ct);

                    await _db.SaveChangesAsync(ct);

                    imported += batch.Count;

                    batch.Clear();
                }
            }
        }

        if (batch.Count > 0)
        {
            await _db.RecipeRecommendations
                .AddRangeAsync(batch, ct);

            await _db.SaveChangesAsync(ct);

            imported += batch.Count;
        }

        return (imported, skipped);
    }

    private static string? FindColumn(
        IEnumerable<string> headers,
        params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var match =
                headers.FirstOrDefault(h =>
                    string.Equals(
                        h,
                        candidate,
                        StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool TryGetInt(
        CsvReader csv,
        string? column,
        out int value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(column))
        {
            return false;
        }

        var raw =
            csv.GetField(column);

        return int.TryParse(raw, out value);
    }

    private static bool TryGetDouble(
        CsvReader csv,
        string? column,
        out double value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(column))
        {
            return false;
        }

        var raw =
            csv.GetField(column);

        return double.TryParse(
            raw,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static IEnumerable<int> ParseIdList(
        string raw)
    {
        return Regex.Matches(raw, @"\d+")
            .Select(m => int.Parse(m.Value))
            .Distinct()
            .ToList();
    }

    private string ResolvePath(
        string? configured,
        string fallback)
    {
        var path =
            string.IsNullOrWhiteSpace(configured)
                ? fallback
                : configured;

        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(
                _env.ContentRootPath,
                path);
    }

    private static string GetSeasonForDate(
        DateTime date)
    {
        var month = date.Month;

        return month switch
        {
            12 or 1 or 2 => "Winter",
            3 or 4 or 5 => "Spring",
            6 or 7 or 8 => "Summer",
            _ => "Fall"
        };
    }
}
