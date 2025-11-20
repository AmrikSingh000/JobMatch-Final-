using System;
using System.IO;
using System.Linq;
using JobMatch.Services.Parsing;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;

namespace JobMatch.Services.Parsing
{
    // All of this is basically about {desc}.
    public class BasicResumeTextExtractor : IResumeTextExtractor
    {
        public string? ExtractText(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                if (ext == ".pdf")
                {
                    return ExtractPdf(filePath);
                }

                if (ext == ".docx")
                {
                    return ExtractDocx(filePath);
                }

                if (ext == ".txt")
                {
                    return File.ReadAllText(filePath);
                }

                // Unknown type  best effort fallback
                return File.ReadAllText(filePath);
            }
            catch
            {
                // if anything blows up, just return null
                return null;
            }
        }

        private string ExtractPdf(string path)
        {
            using var doc = PdfDocument.Open(path);
            var pagesText = doc.GetPages()
                               .Select(p => p.Text)
                               .Where(t => !string.IsNullOrWhiteSpace(t));
            return string.Join(Environment.NewLine, pagesText);
        }

        private string ExtractDocx(string path)
        {
            using var wordDoc = WordprocessingDocument.Open(path, false);
            var body = wordDoc.MainDocumentPart?.Document?.Body;
            if (body == null) return string.Empty;
            return body.InnerText ?? string.Empty;
        }
    }
}
