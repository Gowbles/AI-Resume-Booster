using System.Text.Json;
using AiCvBooster.Models;

namespace AiCvBooster.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  GeminiCvService
// ─────────────────────────────────────────────────────────────────────────────
//  Domain-level implementation of IAiCvService.  It's a *thin* adapter:
//
//      UploadViewModel ──► IAiCvService ──► GeminiCvService ──► GeminiClient
//                                               │
//                                               └── PromptBuilder (prompts)
//                                               └── ParseAnalysis  (JSON → model)
//
//  All retry / network / HTTP concerns live in GeminiClient.  This class only:
//    • assembles the prompts via PromptBuilder
//    • asks the client for a text response
//    • parses that text into a CvAnalysisResult
//    • raises AiServiceException for anything the parse step cannot recover
//
//  That separation is what lets us unit-test GeminiCvService with a fake
//  GeminiClient, and it keeps the UI code catching a single exception type.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class GeminiCvService : IAiCvService
{
    private readonly GeminiClient _client;

    public GeminiCvService(GeminiClient client)
    {
        _client = client;
    }

    public async Task<CvAnalysisResult> AnalyzeAsync(
        AnalysisRequest request,
        CancellationToken ct = default)
    {
        // ── Guard clauses.  Fail fast with a friendly message before we
        //    spend a network round-trip.
        if (request is null)
            throw new AiServiceException(
                AiFailureKind.InvalidRequest,
                "No analysis request was provided.");

        if (string.IsNullOrWhiteSpace(request.OriginalText))
            throw new AiServiceException(
                AiFailureKind.InvalidRequest,
                "The uploaded CV appears to be empty. Please try a different file.");

        // ── 1. Build the prompts (truth-preserving system + user instructions).
        var systemPrompt = PromptBuilder.SystemPrompt;
        var userPrompt = PromptBuilder.BuildUserPrompt(request);

        // ── 2. Ask the transport layer for the model's JSON text output.
        //     GeminiClient already handles retries / timeouts / auth errors
        //     and throws AiServiceException for anything unrecoverable.
        var rawJson = await _client.GenerateTextAsync(systemPrompt, userPrompt, ct)
                                   .ConfigureAwait(false);

        // ── 3. Turn that text into our domain model.  A bad shape here is
        //     always wrapped in AiServiceException — the UI never sees
        //     raw JsonException or InvalidOperationException.
        return ParseAnalysis(rawJson, request.OriginalText);
    }

    /// <summary>
    /// Parses the model-generated JSON blob into a <see cref="CvAnalysisResult"/>.
    /// Any parsing failure is raised as <see cref="AiServiceException"/> with
    /// <see cref="AiFailureKind.InvalidResponse"/> so the UI can show a single,
    /// friendly banner.
    /// </summary>
    private static CvAnalysisResult ParseAnalysis(string json, string originalText)
    {
        // Gemini occasionally wraps its JSON in ```json fences despite the
        // responseMimeType hint — strip them defensively.
        var trimmed = StripCodeFences(json);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(trimmed);
        }
        catch (JsonException jex)
        {
            throw new AiServiceException(
                AiFailureKind.InvalidResponse,
                "Gemini's answer wasn't valid JSON. Please try again.",
                technicalDetail: jex.Message,
                isRetryable: true,
                inner: jex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            int score = 0;
            if (root.TryGetProperty("score", out var scoreEl) &&
                scoreEl.ValueKind == JsonValueKind.Number &&
                scoreEl.TryGetInt32(out var parsedScore))
            {
                score = Math.Clamp(parsedScore, 0, 100);
            }

            var weaknesses = ReadStringArray(root, "weaknesses");
            var keywords = ReadStringArray(root, "keywords");

            string improved = string.Empty;
            if (root.TryGetProperty("improvedCv", out var impEl) &&
                impEl.ValueKind == JsonValueKind.String)
            {
                improved = impEl.GetString() ?? string.Empty;
            }

            // Minimum viable output: we need at least *some* improved text.
            if (string.IsNullOrWhiteSpace(improved))
            {
                throw new AiServiceException(
                    AiFailureKind.InvalidResponse,
                    "Gemini didn't return an improved CV. Please try again.",
                    isRetryable: true);
            }

            return new CvAnalysisResult
            {
                Score = score,
                Weaknesses = weaknesses,
                Keywords = keywords,
                OriginalText = originalText,
                ImprovedText = improved
            };
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>(el.GetArrayLength());
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s!);
            }
        }
        return list;
    }

    private static string StripCodeFences(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var t = s.Trim();
        if (t.StartsWith("```"))
        {
            // Drop the opening fence line (```json or ```) and trailing fence.
            var firstNewline = t.IndexOf('\n');
            if (firstNewline >= 0) t = t[(firstNewline + 1)..];
            if (t.EndsWith("```")) t = t[..^3];
            t = t.Trim();
        }
        return t;
    }
}
