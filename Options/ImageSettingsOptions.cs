namespace SmartKitchen.API.Options;

public class ImageSettingsOptions
{
    public string StorageRoot { get; set; } = @"D:\SmartKitchenImages";

    public string PublicBaseUrl { get; set; } = "https://your-public-server.example.com";
}
