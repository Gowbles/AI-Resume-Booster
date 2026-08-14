using AiCvBooster.Models;

namespace AiCvBooster.Services;

public interface IAiCvService
{
    Task<CvAnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken ct = default);
}
