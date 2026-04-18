namespace AiCvBooster.Models;

public enum CvSource
{
    Pdf,
    Docx,
    Text
}

public sealed class CvDocument
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string RawText { get; init; } = string.Empty;
    public CvSource Source { get; init; }
}
