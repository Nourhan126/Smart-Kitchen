using System.Text;
using SmartKitchen.API.Models;

namespace SmartKitchen.API.Services;

public interface IChatContextService
{
    Task<string> BuildContextAsync(
        string message,
        IReadOnlyList<MediaAsset> attachments,
        CancellationToken ct = default);
}

public class ChatContextService : IChatContextService
{
    public ChatContextService()
    {
    }

    public Task<string> BuildContextAsync(
        string message,
        IReadOnlyList<MediaAsset> attachments,
        CancellationToken ct = default)
    {
        var builder = new StringBuilder();

        if (attachments.Count > 0)
        {
            builder.AppendLine("User uploads:");

            foreach (var asset in attachments)
            {
                builder.AppendLine(
                    $"- {asset.MediaType} file: {asset.Url} ({asset.ContentType}, {asset.SizeBytes} bytes)");

                if (!string.IsNullOrWhiteSpace(asset.ExtractedText))
                {
                    builder.AppendLine("Indexed content:");

                    builder.AppendLine(
                        asset.ExtractedText.Length > 5000
                            ? asset.ExtractedText[..5000]
                            : asset.ExtractedText);
                }
            }
        }

        return Task.FromResult(builder.ToString().Trim());
    }
}
