namespace JobMatch.Services.Parsing
{
    public interface IResumeTextExtractor
    {
        string? ExtractText(string? filePath);
    }
}
