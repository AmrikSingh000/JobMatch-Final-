using JobMatch.Models.ViewModels;

namespace JobMatch.Services.Parsing
{
    public interface IResumeParser
    {
        ResumeParseResult Parse(string resumeText);
    }
}
