using System.Text;
using AiCvBooster.Models;

namespace AiCvBooster.Services;

public static class PromptBuilder
{
    public const string SystemPrompt =
        "You are an elite career coach and professional CV writer with deep expertise in ATS " +
        "(Applicant Tracking Systems), modern recruiting, and resume optimization across industries. " +
        "Your job is to rewrite a candidate's CV so it reads as more professional, clear, impactful, " +
        "and ATS-friendly WITHOUT inventing credentials, employers, dates, or qualifications that are " +
        "not present or reasonably implied in the original. You may rephrase, restructure, quantify " +
        "cautiously, and strengthen verbs. Preserve the candidate's identity and truth. " +
        "Always respond in STRICT JSON matching the requested schema.";

    public static string BuildUserPrompt(AnalysisRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze and rewrite the following CV.");
        sb.AppendLine();
        sb.AppendLine("## Goals");
        sb.AppendLine("- Improve clarity, concision, and professionalism.");
        sb.AppendLine("- Replace weak verbs with strong action verbs (e.g., Led, Architected, Delivered, Reduced, Accelerated).");
        sb.AppendLine("- Where plausible, surface measurable achievements (percentages, scale, time-savings) — NEVER fabricate numbers; if no metric exists, leave qualitative.");
        sb.AppendLine("- Make it ATS-friendly: clean section headings, standard labels (Experience, Education, Skills), no tables/graphics, relevant keywords.");
        sb.AppendLine("- Keep it realistic but impressive. Do not lie or invent.");

        if (request.AggressiveMode)
        {
            sb.AppendLine("- AGGRESSIVE MODE: Be bolder and more assertive. Lead with impact. Use confident, high-agency language. Still truthful.");
        }

        if (!string.IsNullOrWhiteSpace(request.JobDescription))
        {
            sb.AppendLine();
            sb.AppendLine("## Target Job Description");
            sb.AppendLine("Tailor the rewritten CV to emphasize the skills, tools, and outcomes this job cares about. Mirror relevant keywords naturally.");
            sb.AppendLine("```");
            sb.AppendLine(request.JobDescription!.Trim());
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("## Original CV");
        sb.AppendLine("```");
        sb.AppendLine(request.OriginalText);
        sb.AppendLine("```");

        sb.AppendLine();
        sb.AppendLine("## Output");
        sb.AppendLine("Respond with ONLY a JSON object of the form:");
        sb.AppendLine("{");
        sb.AppendLine("  \"score\": <integer 0-100 reflecting the ORIGINAL CV's quality>,");
        sb.AppendLine("  \"weaknesses\": [<short bullet strings describing concrete issues with the original>],");
        sb.AppendLine("  \"keywords\": [<ATS-relevant keywords worth including>],");
        sb.AppendLine("  \"improvedCv\": \"<the full rewritten CV as plain text, using line breaks and clear section headers>\"");
        sb.AppendLine("}");
        sb.AppendLine("Do not include markdown fences or any commentary outside the JSON object.");

        return sb.ToString();
    }
}
