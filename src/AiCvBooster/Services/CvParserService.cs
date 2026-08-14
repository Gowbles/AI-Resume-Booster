using System.IO;
using System.Text;
using AiCvBooster.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace AiCvBooster.Services;

public sealed class CvParserService : ICvParserService
{
    private static readonly string[] SupportedExtensions = { ".pdf", ".docx" };

    public bool IsSupported(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(ext);
    }

    public async Task<CvDocument> ExtractAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("CV file not found.", filePath);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        string text;
        CvSource source;

        switch (ext)
        {
            case ".pdf":
                text = await Task.Run(() => ExtractPdf(filePath), ct).ConfigureAwait(false);
                source = CvSource.Pdf;
                break;
            case ".docx":
                text = await Task.Run(() => ExtractDocx(filePath), ct).ConfigureAwait(false);
                source = CvSource.Docx;
                break;
            default:
                throw new NotSupportedException($"File type '{ext}' is not supported. Please use PDF or DOCX.");
        }

        return new CvDocument
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            RawText = text.Trim(),
            Source = source
        };
    }

    private static string ExtractPdf(string path)
    {
        var sb = new StringBuilder();
        using var pdf = PdfDocument.Open(path);
        foreach (Page page in pdf.GetPages())
        {
            var lines = page.GetWords()
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
                .OrderByDescending(g => g.Key)
                .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));

            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    sb.AppendLine(line);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string ExtractDocx(string path)
    {
        var sb = new StringBuilder();
        using var doc = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document.Body;
        if (body is null) return string.Empty;

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var line = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
            if (!string.IsNullOrWhiteSpace(line))
                sb.AppendLine(line);
        }
        return sb.ToString();
    }
}
