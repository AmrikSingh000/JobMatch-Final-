using System;
using JobMatch.Models.ViewModels;
using JobMatch.Services;

namespace JobMatch.Services.CoverLetters
{
    public class GeminiCoverLetterGenerator : ICoverLetterGenerator
    {
        private readonly GeminiClient _gemini;

        public GeminiCoverLetterGenerator(GeminiClient gemini)
        {
            _gemini = gemini ?? throw new ArgumentNullException(nameof(gemini));
        }

        public string Generate(CoverLetterRequest request)
        {
            var jobTitle = request.JobTitle ?? "the role";
            var company = request.CompanyName ?? request.Company ?? "the company";

            var resumeSummary =
                request.ParsedExperience ??
                request.RawResumeText ??
                string.Empty;

            var prompt = $@"
You are a professional cover letter writer.

Write a tailored cover letter for the job '{jobTitle}' at '{company}'.
Base it on this candidate summary:

{resumeSummary}

If you know the candidate name from the text, use it in the letter. Otherwise, keep the greeting generic.

Requirements:
- 3–5 short paragraphs
- Professional but friendly tone
- Mention relevant experience and skills
- Explain why this candidate is a good fit
- Finish with a simple sign-off (e.g. 'Kind regards').
";

            var letter = _gemini.GenerateAsync("gemini-2.5-pro", prompt)
                                .GetAwaiter()
                                .GetResult();

            return letter;
        }
    }
}
