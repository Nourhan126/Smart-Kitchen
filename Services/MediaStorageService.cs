using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartKitchen.API.Data;
using SmartKitchen.API.Models;
using SmartKitchen.API.Options;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace SmartKitchen.API.Services;

public interface IMediaStorageService
{
    Task<MediaAsset> SaveAsync(IFormFile file, MediaType mediaType, string? userId, CancellationToken ct = default);
    Task<MediaAsset?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<MediaAsset>> GetAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    string GetPhysicalPath(MediaAsset asset);
    string BuildPublicUrl(string relativePath);
}

public class MediaStorageService : IMediaStorageService
{
    private readonly ApplicationDbContext _db;
    private readonly MediaStorageOptions _options;
    private readonly ImageSettingsOptions _imageOptions;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MediaStorageService> _logger;

    private static readonly Dictionary<MediaType, string[]> AllowedExtensions = new()
    {
        [MediaType.Image] = [".jpg", ".jpeg", ".png", ".webp", ".gif"],
        [MediaType.Video] = [".mp4", ".mov", ".avi"],
        [MediaType.Document] =
        [
            ".pdf", ".txt", ".docx", ".doc", ".csv", ".xlsx", ".xls",
            ".pptx", ".ppt", ".json", ".xml", ".rtf", ".md", ".markdown",
            ".log", ".html", ".htm"
        ]
    };

    public MediaStorageService(
        ApplicationDbContext db,
        IOptions<MediaStorageOptions> options,
        IOptions<ImageSettingsOptions> imageOptions,
        IWebHostEnvironment env,
        ILogger<MediaStorageService> logger)
    {
        _db = db;
        _options = options.Value;
        _imageOptions = imageOptions.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<MediaAsset> SaveAsync(
        IFormFile file,
        MediaType mediaType,
        string? userId,
        CancellationToken ct = default)
    {
        ValidateFile(file, mediaType);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}{ext}";
        var relativePath = GetRelativePath(mediaType, storedFileName);
        var physicalPath = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

        await using (var stream = File.Create(physicalPath))
        {
            await file.CopyToAsync(stream, ct);
        }

        var extractedText = await ExtractTextAsync(physicalPath, ext, ct);

        var asset = new MediaAsset
        {
            UserId = userId,
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            ContentType = file.ContentType,
            MediaType = mediaType,
            SizeBytes = file.Length,
            RelativePath = relativePath.Replace("\\", "/"),
            Url = BuildPublicUrl(relativePath),
            ExtractedText = extractedText,
            ExtractedAt = string.IsNullOrWhiteSpace(extractedText)
                ? null
                : DateTime.UtcNow
        };

        _db.MediaAssets.Add(asset);
        await _db.SaveChangesAsync(ct);

        return asset;
    }

    public async Task<MediaAsset?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.MediaAssets.FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<List<MediaAsset>> GetAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return new List<MediaAsset>();
        }

        return await _db.MediaAssets
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(ct);
    }

    public string GetPhysicalPath(MediaAsset asset)
    {
        return Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, asset.RelativePath);
    }

    public string BuildPublicUrl(string relativePath)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');

        if (IsLocalOrMachineNameUrl(baseUrl))
        {
            baseUrl = _imageOptions.PublicBaseUrl.TrimEnd('/');
        }

        var normalized = relativePath.Replace("\\", "/").TrimStart('/');
        return $"{baseUrl}/{normalized}";
    }

    private static bool IsLocalOrMachineNameUrl(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Contains('.') && !System.Net.IPAddress.TryParse(uri.Host, out _);
    }

    private string GetRelativePath(MediaType mediaType, string fileName)
    {
        var folder = mediaType switch
        {
            MediaType.Image => _options.ImagesPath,
            MediaType.Video => _options.VideosPath,
            _ => _options.DocumentsPath
        };

        return Path.Combine(folder, fileName);
    }

    private void ValidateFile(IFormFile file, MediaType mediaType)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.TryGetValue(mediaType, out var allowed) || !allowed.Contains(ext))
        {
            throw new InvalidOperationException("Unsupported file type.");
        }

        var maxBytes = mediaType switch
        {
            MediaType.Image => _options.MaxImageBytes,
            MediaType.Video => _options.MaxVideoBytes,
            _ => _options.MaxDocumentBytes
        };

        if (file.Length > maxBytes)
        {
            throw new InvalidOperationException("File exceeds allowed size.");
        }
    }

    private static async Task<string?> ExtractTextAsync(
        string path,
        string extension,
        CancellationToken ct)
    {
        try
        {
            return extension switch
            {
                ".pdf" => ExtractPdfText(path),
                ".docx" => ExtractOpenXmlText(path, "word/document.xml"),
                ".xlsx" => ExtractOpenXmlText(path, "xl/sharedStrings.xml"),
                ".pptx" => ExtractOpenXmlText(path, "ppt/slides/"),
                ".txt" or ".csv" or ".json" or ".xml" or ".rtf" or ".md" or
                ".markdown" or ".log" or ".html" or ".htm" =>
                    CleanExtractedText(await File.ReadAllTextAsync(path, ct)),
                ".doc" or ".xls" or ".ppt" =>
                    CleanExtractedText(await ReadLegacyBinaryAsTextAsync(path, ct)),
                _ => null
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ExtractPdfText(string path)
    {
        using var document = PdfDocument.Open(path);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return CleanExtractedText(builder.ToString());
    }

    private static string ExtractOpenXmlText(
        string path,
        string entryPrefix)
    {
        using var archive = ZipFile.OpenRead(path);
        var builder = new StringBuilder();

        foreach (var entry in archive.Entries
            .Where(e =>
                e.FullName.StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase) &&
                e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var xml = reader.ReadToEnd();
            builder.AppendLine(Regex.Replace(xml, "<[^>]+>", " "));
        }

        return CleanExtractedText(builder.ToString());
    }

    private static async Task<string> ReadLegacyBinaryAsTextAsync(
        string path,
        CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var text = Encoding.UTF8.GetString(bytes);
        return Regex.Replace(text, @"[^\u0009\u000A\u000D\u0020-\u007E]+", " ");
    }

    private static string CleanExtractedText(string text)
    {
        return Regex.Replace(text, @"\s+", " ")
            .Trim();
    }
}
