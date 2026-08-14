namespace AiCvBooster.Models;

public sealed class CvAnalysisResult
{
    public int Score { get; init; }
    public IReadOnlyList<string> Weaknesses { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public string OriginalText { get; init; } = string.Empty;
    public string ImprovedText { get; init; } = string.Empty;
}
