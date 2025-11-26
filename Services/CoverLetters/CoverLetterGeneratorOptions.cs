namespace JobMatch.Services.CoverLetters
{
    // This part mostly deals with options used when building cover letters.
    public sealed class CoverLetterGeneratorOptions
    {
        
        public string Model { get; set; } = "gemini-1.5-flash";

    
        public double Temperature { get; set; } = 0.3;

       
        public int MaxTokens { get; set; } = 900;

       
        public string? ApiBase { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

       
        public string? ApiKey { get; set; }
    }
}
