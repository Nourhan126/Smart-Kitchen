namespace SmartKitchen.API.Options;

public class MediaStorageOptions
{
    public string BaseUrl { get; set; } = "https://YOUR_PUBLIC_SERVER";
    public string ImagesPath { get; set; } = "uploads/images";
    public string VideosPath { get; set; } = "uploads/videos";
    public string DocumentsPath { get; set; } = "uploads/files";
    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;
    public long MaxVideoBytes { get; set; } = 50 * 1024 * 1024;
    public long MaxDocumentBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxInlineImageBytes { get; set; } = 2 * 1024 * 1024;
}
