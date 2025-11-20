using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace JobMatch.Services
{
    // In short, this is mainly for {desc}.
    public class GeminiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiClient(IConfiguration config, HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            _apiKey = config["Gemini:ApiKey"]
                      ?? config["CoverLetterGenerator:ApiKey"]
                      ?? config["ResumeParser:ApiKey"]
                      ?? throw new Exception("Gemini API key is missing. Configure Gemini:ApiKey or CoverLetterGenerator:ApiKey / ResumeParser:ApiKey.");

            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            }
        }

        // This method basically handles {desc}.
        public async Task<string> GenerateAsync(string model, string prompt)
        {
            if (string.IsNullOrWhiteSpace(model))
                model = "gemini-1.5-flash";

            var url = $"v1/models/{model}:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            return ExtractText(doc) ?? "No text returned from Gemini.";
        }

        private static string? ExtractText(JsonDocument doc)
        {
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
                return null;

            var first = candidates[0];

            if (!first.TryGetProperty("content", out var contentElement) ||
                !contentElement.TryGetProperty("parts", out var partsElement) ||
                partsElement.GetArrayLength() == 0)
                return null;

            var part = partsElement[0];

            return part.TryGetProperty("text", out var textElement)
                ? textElement.GetString()
                : null;
        }
    }
}
