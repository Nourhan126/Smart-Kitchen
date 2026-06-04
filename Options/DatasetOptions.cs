namespace SmartKitchen.API.Options;

public class DatasetOptions
{
    public string? RecipeCsvPath { get; set; }

    public string? ImageMetadataCsvPath { get; set; }

    public string? ProgressJsonPath { get; set; }

    public string? ImagesRootPath { get; set; }

    public string? RecommendationZipPath { get; set; }

    public string? SeasonalRecommendationZipPath { get; set; }

    public string? RecommendationPath { get; set; }

    public string? SeasonalRecommendationPath { get; set; }
}
