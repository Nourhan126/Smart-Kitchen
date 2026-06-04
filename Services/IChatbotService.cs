namespace SmartKitchen.API.Services;
public interface IChatbotService
{
    Task<string> ChatAsync(
        string userMessage,
        string? userId = null,
        IReadOnlyList<Models.MediaAsset>? attachments = null,
        CancellationToken ct = default);
}
