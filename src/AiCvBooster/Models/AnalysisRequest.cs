namespace AiCvBooster.Models;

public sealed class AnalysisRequest
{
    public string OriginalText { get; init; } = string.Empty;
    public string? JobDescription { get; init; }
    public bool AggressiveMode { get; init; }
}
