using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SmartKitchen.API.Models;
using SmartKitchen.API.Options;

namespace SmartKitchen.API.Services;

public class ChatbotService : IChatbotService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ChatbotService> _logger;
    private readonly IMediaStorageService _mediaStorage;
    private readonly IChatContextService _contextService;
    private readonly MediaStorageOptions _mediaOptions;

    private readonly string SystemPrompt =
    "You are an intelligent assistant for a Smart Kitchen application. " +
    "Your role is to help users with cooking, recipes, ingredients, and meal planning, " +
    "as well as general kitchen-related questions such as appliance usage, gas safety, and troubleshooting. " +
    "If the question is about food, provide structured and helpful cooking guidance. " +
    "If the question is about safety (e.g., gas leaks), prioritize safety instructions first. " +
    "If the question is general, provide accurate and simple explanations. " +
    "Use dataset context when provided, but keep responses natural and conversational. " +
    "When the user attaches an image, treat the visible image content as the primary source. " +
    "Do not invent screens, sections, controls, recipes, or UI states that are not visible in the image. " +
    "Return plain mobile-friendly text only: no markdown, no headings syntax, no bullets, no tables, and no code fences. " +
    "Always respond in a clear, concise, and helpful way.";
    public ChatbotService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<ChatbotService> logger,
        IMediaStorageService mediaStorage,
        IChatContextService contextService,
        IOptions<MediaStorageOptions> mediaOptions)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
        _mediaStorage = mediaStorage;
        _contextService = contextService;
        _mediaOptions = mediaOptions.Value;
    }

    public async Task<string> ChatAsync(
        string userMessage,
        string? userId = null,
        IReadOnlyList<MediaAsset>? attachments = null,
        CancellationToken ct = default)
    {
        var apiKey = _config["Gemini:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            return "AI assistant is not configured. Please contact support.";

        try
        {
            var client = _httpClientFactory.CreateClient();

            attachments ??= Array.Empty<MediaAsset>();
            var context = await _contextService
                .BuildContextAsync(userMessage, attachments, ct);

            var parts = new List<object>
            {
                new
                {
                    text = string.IsNullOrWhiteSpace(context)
                        ? $"{SystemPrompt}\nUser: {userMessage}"
                        : $"{SystemPrompt}\n{context}\nUser: {userMessage}"
                }
            };

            foreach (var asset in attachments
                .Where(a => a.MediaType == MediaType.Image)
                .Take(3))
            {
                if (asset.SizeBytes > _mediaOptions.MaxInlineImageBytes)
                {
                    continue;
                }

                var path = _mediaStorage.GetPhysicalPath(asset);
                if (!File.Exists(path))
                {
                    continue;
                }

                var bytes = await File.ReadAllBytesAsync(path, ct);
                var base64 = Convert.ToBase64String(bytes);
                parts.Add(new
                {
                    inline_data = new
                    {
                        mime_type = asset.ContentType,
                        data = base64
                    }
                });
            }

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

          
            var url =
                $"https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash:generateContent?key={apiKey}";

            var response = await client.PostAsync(url, content, ct);

            var responseJson = await response.Content.ReadAsStringAsync(ct);

            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini Error: {Error}", responseJson);
                return $"Gemini Error: {responseJson}";
            }

            using var doc = JsonDocument.Parse(responseJson);

            var reply = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return CleanReply(reply ?? "Sorry, I couldn't generate a response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
            return CleanReply($"Exception: {ex.Message}");
        }
    }

    private static string CleanReply(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return string.Empty;
        }

        var cleaned = reply
            .Replace("\\r\\n", Environment.NewLine)
            .Replace("\\n", Environment.NewLine)
            .Replace("\\r", string.Empty)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        cleaned = Regex.Replace(cleaned, @"```[\s\S]*?```", m =>
            m.Value.Replace("```", string.Empty));

        cleaned = Regex.Replace(cleaned, @"^#{1,6}\s*", "", RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"^\s*[-*+]\s+", "", RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"^\s*>\s?", "", RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @"[*_`#]+", "");
        cleaned = Regex.Replace(cleaned, @"-{3,}", "");
        cleaned = Regex.Replace(cleaned, @"[ \t]{2,}", " ");
        cleaned = Regex.Replace(cleaned, @"\n[ \t]+", "\n");
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");

        return cleaned.Trim();
    }
}
