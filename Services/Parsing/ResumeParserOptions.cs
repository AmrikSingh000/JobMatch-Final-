namespace JobMatch.Services.Parsing
{
    // This bit is here to handle {desc}.
    public sealed class ResumeParserOptions
    {
        /// <summary>
        /// </summary>
        public string Model { get; set; } = "gemini-1.5-flash";

        /// <summary>
        /// Sampling temperature for parsing. Usually you want this low for deterministic JSON.
        /// </summary>
        public double Temperature { get; set; } = 0.0;

        /// <summary>
        /// Maximum number of tokens to generate in the JSON response.
        /// </summary>
        public int MaxTokens { get; set; } = 700;

        /// <summary>
        /// </summary>
        public string ApiBase { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

        /// <summary>
        /// not hard-code it in source.
        /// </summary>
        public string? ApiKey { get; set; }
    }
}
