namespace SmartKitchen.API.DTOs;

public class DatasetFileStatus
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class DatasetValidationResult
{
    public bool IsValid => Files.All(f => f.IsValid);
    public List<DatasetFileStatus> Files { get; set; } = new();
}

public class DatasetIngestionResult
{
    public int RecipesSeeded { get; set; }
    public int ImagesSynced { get; set; }
}

public class RecommendationImportResult
{
    public int TotalImported { get; set; }
    public int TotalSkipped { get; set; }
    public int TotalSources { get; set; }
}
