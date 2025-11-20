using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JobMatch.Models.ViewModels;

namespace JobMatch.Services.Parsing
{
    // This chunk takes care of {desc}.
    public class GeminiResumeParser : IResumeParser
    {
        private readonly GeminiClient _gemini;

        public GeminiResumeParser(GeminiClient gemini)
        {
            _gemini = gemini ?? throw new ArgumentNullException(nameof(gemini));
        }

        // Roughly speaking, this is for {desc}.
        public ResumeParseResult Parse(string resumeText)
        {
            resumeText ??= string.Empty;

            var result = new ResumeParseResult
            {
                UsedAi = true,
                Engine = "Gemini",
                Model = "gemini-2.5-flash"
            };

            var prompt = $@"
You are a strict JSON generator.

Given the resume text below, output ONLY JSON with EXACTLY these keys:
- name: string or null
- email: string or null
- skills: array of short skill keywords (e.g. ""C#"", ""ASP.NET"", ""SQL"", ""React"")
- experience: string or null
- education: string or null

Do NOT include explanations, markdown, or any extra text outside the JSON.

Resume:
{resumeText}
";

            string raw;
            try
            {
                // avoid deadlocks by running the async call on a separate task
                raw = Task.Run(async () =>
                    await _gemini.GenerateAsync("gemini-2.5-flash", prompt)
                ).Result;
            }
            catch (Exception ex)
            {
                // if the API completely fails, fall back to local parsing only
                raw = string.Empty;
                result.Engine = $"Gemini error: {ex.Message}";
            }

            raw = raw?.Trim() ?? string.Empty;

            if (raw.StartsWith("```"))
            {
                raw = raw.Trim('`').Trim();

                // remove possible leading "json"
                if (raw.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                {
                    raw = raw.Substring(4).Trim();
                }
            }

            // try to salvage a JSON object from the response
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var jsonMatch = Regex.Match(raw, @"\{(.|\n)*\}");
                if (jsonMatch.Success)
                {
                    raw = jsonMatch.Value;
                }
            }

            //  Try to parse JSON into ResumeParseResult 
            try
            {
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    using var doc = JsonDocument.Parse(raw);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("name", out var nameProp))
                        result.Name = nameProp.GetString();

                    if (root.TryGetProperty("email", out var emailProp))
                        result.Email = emailProp.GetString();

                    if (root.TryGetProperty("skills", out var skillsProp) &&
                        skillsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in skillsProp.EnumerateArray())
                        {
                            var skill = s.GetString();
                            if (!string.IsNullOrWhiteSpace(skill))
                                result.Skills.Add(skill.Trim());
                        }
                    }

                    if (root.TryGetProperty("experience", out var expProp))
                        result.Experience = expProp.GetString();

                    if (root.TryGetProperty("education", out var eduProp))
                        result.Education = eduProp.GetString();
                }
            }
            catch
            {
                // ignore JSON errors here; we'll fall back below
            }

            
            if (result.Skills.Count == 0)
            {
                var textLower = resumeText.ToLowerInvariant();

                // quick list of common dev skills / tech keywords
                var knownSkills = new[]
                {
                    "c#", ".net", "asp.net", "mvc", "entity framework",
                    "sql", "mysql", "postgresql", "sqlite",
                    "javascript", "typescript", "react", "angular", "vue",
                    "html", "css", "tailwind", "bootstrap",
                    "python", "java", "spring", "node", "node.js", "express",
                    "php", "laravel", "django", "flask",
                    "azure", "aws", "gcp", "docker", "kubernetes",
                    "git", "github", "gitlab",
                    "rest", "api", "web api", "microservices",
                    "xunit", "nunit", "unit testing"
                };

                foreach (var kw in knownSkills)
                {
                    if (textLower.Contains(kw.ToLowerInvariant()) &&
                        !result.Skills.Contains(kw, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Skills.Add(kw);
                    }
                }
            }

            //   If experience is still empty, at least store some resume text 
            if (string.IsNullOrWhiteSpace(result.Experience))
            {
                result.Experience = resumeText.Length > 2000
                    ? resumeText.Substring(0, 2000)
                    : resumeText;
            }

            return result;
        }
    }
}
