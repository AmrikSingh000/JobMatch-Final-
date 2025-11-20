using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using JobMatch.Models.ViewModels;
using JobMatch.Services.CoverLetters;
using JobMatch.Services.Parsing;

namespace JobMatch.Controllers
{
    public class ResumesController : Controller
    {
        private readonly IResumeParser _resumeParser;
        private readonly ICoverLetterGenerator _coverLetterGenerator;

        public ResumesController(
            IResumeParser resumeParser,
            ICoverLetterGenerator coverLetterGenerator)
        {
            _resumeParser = resumeParser;
            _coverLetterGenerator = coverLetterGenerator;
        }

        [HttpGet]
        public IActionResult Upload()
        {
            // Simple upload form view (you can create Views/Resumes/Upload.cshtml if you want)
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile resumeFile, string jobTitle, string companyName)
        {
            if (resumeFile == null || resumeFile.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please upload a resume file.");
                return View();
            }

            string resumeText;
            using (var reader = new StreamReader(resumeFile.OpenReadStream()))
            {
                resumeText = await reader.ReadToEndAsync();
            }

            var parsed = _resumeParser.Parse(resumeText);

            // 2) Build a CoverLetterRequest object for the generator
            var request = new CoverLetterRequest
            {
                JobTitle = jobTitle,
                CompanyName = companyName,
                RawResumeText = resumeText,
                ParsedName = parsed.Name,
                ParsedEmail = parsed.Email,
                ParsedSkills = (parsed.Skills != null && parsed.Skills.Count > 0)
                    ? string.Join(", ", parsed.Skills)
                    : null,
                ParsedExperience = parsed.Experience,
                ParsedEducation = parsed.Education
            };

            var coverLetter = _coverLetterGenerator.Generate(request);

            // 4) Pass data to view
            ViewBag.ParsedResume = parsed;
            ViewBag.CoverLetter = coverLetter;

            // Expect a view at Views/Resumes/Result.cshtml (or change this name)
            return View("Result");
        }
    }
}
