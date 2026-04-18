using AiCvBooster.Models;

namespace AiCvBooster.Services;

public interface ICvParserService
{
    Task<CvDocument> ExtractAsync(string filePath, CancellationToken ct = default);
    bool IsSupported(string filePath);
}
