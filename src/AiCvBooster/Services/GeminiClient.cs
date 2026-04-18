using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AiCvBooster.Models;
using Microsoft.Extensions.Options;

namespace AiCvBooster.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  GeminiClient
// ─────────────────────────────────────────────────────────────────────────────
//  A self-contained transport wrapper around the Google Generative Language
//  REST API.  It does ONE thing: "given a system prompt + user prompt, give me
//  back the model's text output".
//
//  This class intentionally has NO dependency on any ViewModel, CV parser, or
//  WPF type — you could lift it into a console app or a unit-test harness
//  without touching a line.  Everything UI-facing lives in GeminiCvService.
//
//  Responsibilities:
//    • Build the HTTP request (URL, JSON body, system instruction, JSON mode).
//    • Retry transient failures with exponential backoff (429, 5xx, timeouts).
//    • Translate raw HTTP / parsing issues into AiServiceException with a
//      friendly message + a technical detail + an IsRetryable flag.
//    • Extract the first text part from the Gemini response envelope.
//
//  Non-responsibilities:
//    • Interpreting the text as JSON and mapping it to domain models
//      (that's GeminiCvService.ParseAnalysis).
//    • Talking to the UI.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class GeminiClient
{
    private readonly HttpClient _http;
    private readonly GeminiOptions _options;

    // Retry schedule for transient errors.  Short enough to keep the UX snappy,
    // long enough to let an intermittent rate-limit clear.
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(6)
    };

    public GeminiClient(HttpClient http, IOptions<AppSettings> settings)
    {
        _http = http;
        _options = settings.Value.Gemini;
    }

    /// <summary>
    /// Sends a single prompt to Gemini and returns the raw text the model
    /// produced.  All transient errors are retried transparently; anything
    /// that survives the retries is raised as an <see cref="AiServiceException"/>
    /// carrying a human-readable message.
    /// </summary>
    public async Task<string> GenerateTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken ct = default)
    {
        // ── Validate configuration up-front so the user gets an actionable
        //    message instead of a 400 from the server.
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new AiServiceException(
                AiFailureKind.Authentication,
                "Gemini API key is missing. Open appsettings.json and set \"Gemini:ApiKey\".",
                technicalDetail: "GeminiOptions.ApiKey is empty.");
        }

        // ── Build request once; reuse across retries via a factory lambda
        //    because HttpRequestMessage instances are single-use.
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/models/{_options.Model}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";

        var payload = new
        {
            systemInstruction = new
            {
                parts = new object[] { new { text = systemPrompt } }
            },
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[] { new { text = userPrompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.5,
                responseMimeType = "application/json"
            }
        };

        // ── Retry loop.  Each iteration:
        //    1. Sends the request
        //    2. Examines the outcome
        //    3. Either returns, waits + retries, or throws a friendly error.
        Exception? lastError = null;
        for (int attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(payload)
                };

                using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    // Happy path — pull the text out of the envelope.
                    return ExtractResponseText(body);
                }

                // Non-2xx: decide whether to retry or give up.
                var failure = MapHttpFailure(response.StatusCode, body);

                if (failure.IsRetryable && attempt < RetryDelays.Length)
                {
                    lastError = failure;
                    await Task.Delay(RetryDelays[attempt], ct).ConfigureAwait(false);
                    continue;
                }

                throw failure;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Genuine user cancellation — propagate, caller handles it.
                throw new AiServiceException(
                    AiFailureKind.Cancelled,
                    "Analysis was cancelled.");
            }
            catch (TaskCanceledException tcex)
            {
                // HttpClient timeout masquerades as TaskCanceledException when
                // the token itself wasn't cancelled.
                lastError = tcex;
                if (attempt < RetryDelays.Length)
                {
                    await Task.Delay(RetryDelays[attempt], ct).ConfigureAwait(false);
                    continue;
                }
                throw new AiServiceException(
                    AiFailureKind.Network,
                    "The request to Gemini timed out. Check your connection and try again.",
                    technicalDetail: tcex.Message,
                    isRetryable: true,
                    inner: tcex);
            }
            catch (HttpRequestException hrex)
            {
                lastError = hrex;
                if (attempt < RetryDelays.Length)
                {
                    await Task.Delay(RetryDelays[attempt], ct).ConfigureAwait(false);
                    continue;
                }
                throw new AiServiceException(
                    AiFailureKind.Network,
                    "Could not reach Gemini. Please check your internet connection.",
                    technicalDetail: hrex.Message,
                    isRetryable: true,
                    inner: hrex);
            }
            catch (AiServiceException)
            {
                // Already friendly — let it bubble.
                throw;
            }
            catch (Exception ex)
            {
                // Anything truly unexpected: wrap, don't leak.
                lastError = ex;
                throw new AiServiceException(
                    AiFailureKind.Unknown,
                    "Something went wrong while contacting Gemini.",
                    technicalDetail: ex.Message,
                    inner: ex);
            }
        }

        // Theoretically unreachable — the loop either returns or throws — but
        // this keeps the compiler happy and documents the contract.
        throw new AiServiceException(
            AiFailureKind.Unknown,
            "Gemini did not return a successful response after several retries.",
            technicalDetail: lastError?.Message);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Failure mapping
    // ─────────────────────────────────────────────────────────────────────
    //  Translate an HTTP status code + body into a typed AiServiceException.
    //  Messages are deliberately human-readable and avoid echoing raw JSON.
    // ─────────────────────────────────────────────────────────────────────
    private static AiServiceException MapHttpFailure(HttpStatusCode status, string body)
    {
        var shortDetail = Truncate(body, 300);

        return (int)status switch
        {
            400 => new AiServiceException(
                AiFailureKind.InvalidRequest,
                "Gemini rejected the request. The CV may be too long or contain unsupported content.",
                technicalDetail: shortDetail),

            401 or 403 => new AiServiceException(
                AiFailureKind.Authentication,
                "Your Gemini API key was rejected. Double-check the key in appsettings.json.",
                technicalDetail: shortDetail),

            404 => new AiServiceException(
                AiFailureKind.InvalidRequest,
                "The configured Gemini model was not found. Try \"gemini-1.5-flash\" or \"gemini-2.0-flash\".",
                technicalDetail: shortDetail),

            429 => new AiServiceException(
                AiFailureKind.RateLimited,
                "Gemini rate limit reached. Please wait a moment and try again.",
                technicalDetail: shortDetail,
                isRetryable: true),

            >= 500 and <= 599 => new AiServiceException(
                AiFailureKind.ServerError,
                "Gemini is temporarily unavailable. We'll retry — please hold on.",
                technicalDetail: shortDetail,
                isRetryable: true),

            _ => new AiServiceException(
                AiFailureKind.Unknown,
                $"Gemini returned an unexpected response ({(int)status}).",
                technicalDetail: shortDetail)
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Response envelope parsing
    // ─────────────────────────────────────────────────────────────────────
    //  Pulls the first text part out of the Gemini response:
    //    { candidates: [ { content: { parts: [ { text: "..." } ] } } ] }
    // ─────────────────────────────────────────────────────────────────────
    private static string ExtractResponseText(string body)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException jex)
        {
            throw new AiServiceException(
                AiFailureKind.InvalidResponse,
                "Gemini returned a response we couldn't read.",
                technicalDetail: jex.Message,
                inner: jex);
        }

        using (doc)
        {
            var root = doc.RootElement;

            // Prompt was blocked by safety filters.
            if (root.TryGetProperty("promptFeedback", out var feedback) &&
                feedback.TryGetProperty("blockReason", out var blockReason))
            {
                throw new AiServiceException(
                    AiFailureKind.InvalidRequest,
                    "Gemini blocked this request for safety reasons. Try rephrasing the CV or the job description.",
                    technicalDetail: blockReason.GetString());
            }

            if (!root.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array ||
                candidates.GetArrayLength() == 0)
            {
                throw new AiServiceException(
                    AiFailureKind.InvalidResponse,
                    "Gemini returned an empty response. Please try again.",
                    technicalDetail: Truncate(body, 300));
            }

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var contentNode) ||
                    contentNode.ValueKind != JsonValueKind.Object)
                    continue;

                if (!contentNode.TryGetProperty("parts", out var parts) ||
                    parts.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textNode) &&
                        textNode.ValueKind == JsonValueKind.String)
                    {
                        var text = textNode.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                            return text!;
                    }
                }
            }

            throw new AiServiceException(
                AiFailureKind.InvalidResponse,
                "Gemini's response didn't contain any text. Please try again.",
                technicalDetail: Truncate(body, 300));
        }
    }

    private static string Truncate(string s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";
}
