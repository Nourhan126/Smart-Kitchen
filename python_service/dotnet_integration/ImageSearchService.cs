// ─────────────────────────────────────────────────────────────────────────────
// ImageSearchService.cs
// Service layer that wraps calls to the Python image-collection microservice.
// Register as a typed HttpClient in Program.cs (see README for wiring).
// ─────────────────────────────────────────────────────────────────────────────

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartKitchen.API.Options;

namespace SmartKitchen.API.Services;

// ── Request / Response DTOs ───────────────────────────────────────────────────

public record ImageSearchRequest(
    [property: JsonPropertyName("recipe_name")] string RecipeName,
    [property: JsonPropertyName("target_type")] string TargetType = "recipe",
    [property: JsonPropertyName("context")] string? Context = null
);

public record ImageSearchResponse(
    [property: JsonPropertyName("recipe_name")]  string  RecipeName,
    [property: JsonPropertyName("target_type")]  string  TargetType,
    [property: JsonPropertyName("image_path")]   string? ImagePath,
    [property: JsonPropertyName("image_url")]    string? ImageUrl,
    [property: JsonPropertyName("success")]      bool    Success,
    [property: JsonPropertyName("error")]        string? Error
);

// ── Service interface ─────────────────────────────────────────────────────────

public interface IImageSearchService
{
    /// <summary>Search and download an image for a single recipe name.</summary>
    Task<ImageSearchResponse?> SearchImageAsync(
        string recipeName,
        string targetType = "recipe",
        string? context = null,
        CancellationToken ct = default);

    /// <summary>Check whether the Python service is healthy.</summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

public interface IImageUrlBuilder
{
    string BuildPublicImageUrl(ImageSearchResponse result);

    string? NormalizeImageUrl(string? imageUrl, string targetType);
}

public class ImageUrlBuilder : IImageUrlBuilder
{
    private readonly ImageSettingsOptions _options;
    private readonly IWebHostEnvironment _env;

    public ImageUrlBuilder(
        IOptions<ImageSettingsOptions> options,
        IWebHostEnvironment env)
    {
        _options = options.Value;
        _env = env;
    }

    public string BuildPublicImageUrl(ImageSearchResponse result)
    {
        var targetFolder = GetTargetFolder(result.TargetType);
        var fileName = ExtractFileName(result.ImagePath) ??
            ExtractFileName(result.ImageUrl);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException(
                "Image service returned no usable image file name.");
        }

        CopyImageToPublicStorage(result.ImagePath, targetFolder, fileName);

        return BuildUrl(targetFolder, fileName);
    }

    public string? NormalizeImageUrl(string? imageUrl, string targetType)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) ||
            imageUrl.Contains("drive.google", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = imageUrl.Trim().Replace("\\", "/");
        var targetFolder = GetTargetFolder(DetectTargetType(normalized) ?? targetType);
        var isBackendImagePath =
            normalized.Contains("/uploads/recipe/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/uploads/ingredient/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/uploads/step/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/images/recipe/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/images/ingredient/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/images/step/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("dataset/images/", StringComparison.OrdinalIgnoreCase);

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            if (!IsLocalHost(uri.Host) && !isBackendImagePath)
            {
                return null;
            }

            var uriPath = Uri.UnescapeDataString(uri.AbsolutePath);
            var fileNameFromUri = ExtractFileName(uriPath);

            return string.IsNullOrWhiteSpace(fileNameFromUri)
                ? null
                : BuildUrl(targetFolder, fileNameFromUri);
        }

        var fileName = ExtractFileName(normalized);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        if (Path.IsPathRooted(imageUrl))
        {
            CopyImageToPublicStorage(imageUrl, targetFolder, fileName);
        }

        return BuildUrl(targetFolder, fileName);
    }

    private void CopyImageToPublicStorage(
        string? sourcePath,
        string targetFolder,
        string fileName)
    {
        var source = ResolveExistingPath(sourcePath);
        var targetDirectory = Path.Combine(_options.StorageRoot, targetFolder);
        var targetPath = Path.Combine(targetDirectory, fileName);

        Directory.CreateDirectory(targetDirectory);

        if (source is null ||
            File.Exists(targetPath) ||
            Path.GetFullPath(source).Equals(
                Path.GetFullPath(targetPath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.Copy(source, targetPath, overwrite: false);
    }

    private string? ResolveExistingPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var candidates = new List<string>();

        if (Path.IsPathRooted(path))
        {
            candidates.Add(path);
        }
        else
        {
            candidates.Add(Path.Combine(_env.ContentRootPath, path));
            candidates.Add(Path.Combine(
                _env.ContentRootPath,
                "python_service",
                path));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private string BuildUrl(string targetFolder, string fileName)
    {
        var publicBaseUrl = _options.PublicBaseUrl.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(publicBaseUrl) ||
            !Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var uri) ||
            IsLocalHost(uri.Host))
        {
            throw new InvalidOperationException(
                "ImageSettings:PublicBaseUrl must be a public server URL or backend machine IP, not localhost or a machine name.");
        }

        return $"{publicBaseUrl}/uploads/{targetFolder}/{Uri.EscapeDataString(fileName)}";
    }

    private static string GetTargetFolder(string? targetType)
    {
        return NormalizeTargetType(targetType) switch
        {
            "ingredient" => "ingredient",
            "step" => "step",
            _ => "recipe"
        };
    }

    private static string NormalizeTargetType(string? targetType)
    {
        var value = (targetType ?? "recipe").Trim().ToLowerInvariant();

        return value.TrimEnd('s') switch
        {
            "ingredient" => "ingredient",
            "step" => "step",
            _ => "recipe"
        };
    }

    private static string? DetectTargetType(string path)
    {
        if (path.Contains("/ingredients/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/ingredient/", StringComparison.OrdinalIgnoreCase))
        {
            return "ingredient";
        }

        if (path.Contains("/steps/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/step/", StringComparison.OrdinalIgnoreCase))
        {
            return "step";
        }

        if (path.Contains("/recipes/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/recipe/", StringComparison.OrdinalIgnoreCase))
        {
            return "recipe";
        }

        return null;
    }

    private static string? ExtractFileName(string? pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
        {
            return null;
        }

        var value = pathOrUrl.Trim().Replace("\\", "/").TrimEnd('/');

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            value = Uri.UnescapeDataString(uri.AbsolutePath.TrimEnd('/'));
        }

        var fileName = Path.GetFileName(value);

        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName;
    }

    private static bool IsLocalHost(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
            !host.Contains('.') && !System.Net.IPAddress.TryParse(host, out _);
    }
}

// ── Service implementation ────────────────────────────────────────────────────

public class ImageSearchService : IImageSearchService
{
    private readonly HttpClient        _http;
    private readonly ILogger<ImageSearchService> _logger;

    // Injected via DI – see Program.cs for HttpClient registration
    public ImageSearchService(HttpClient http, ILogger<ImageSearchService> logger)
    {
        _http   = http;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ImageSearchResponse?> SearchImageAsync(
        string            recipeName,
        string            targetType = "recipe",
        string?           context = null,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new ImageSearchRequest(recipeName, targetType, context);
            var response = await _http.PostAsJsonAsync("/search-image", payload, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ImageSearchResponse>(
                cancellationToken: ct
            );
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch image for recipe '{RecipeName}'", recipeName);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("/health", ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
