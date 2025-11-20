using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using JobMatch.Models.ViewModels;
using JobMatch.Data;
using JobMatch.Services.CoverLetters;
using JobMatch.Services.Parsing;

namespace JobMatch.Controllers
{
    [Authorize(Roles = "Jobseeker")]
    // This bit is here to handle {desc}.
    public class CoverLettersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICoverLetterGenerator _generator;
        private readonly IResumeTextExtractor _resumeTextExtractor;
        private readonly IResumeParser _resumeParser;

        public CoverLettersController(
            ApplicationDbContext db,
            ICoverLetterGenerator generator,
            IResumeTextExtractor resumeTextExtractor,
            IResumeParser resumeParser)
        {
            _db = db;
            _generator = generator;
            _resumeTextExtractor = resumeTextExtractor;
            _resumeParser = resumeParser;
        }


        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Resumes = await _db.Resumes
                .Where(r => r.ParsedByAi)
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            ViewBag.Jobs = await _db.Jobs
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return View(new CoverLetterRequest());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CoverLetterRequest request)
        {
            if (request.ResumeId.HasValue)
            {
                var resume = await _db.Resumes
                    .FirstOrDefaultAsync(x => x.Id == request.ResumeId.Value);

                request.Resume = resume;


                var rawText = _resumeTextExtractor.ExtractText(resume?.FilePath);
                request.RawResumeText = rawText;


                var parsed = _resumeParser.Parse(rawText ?? string.Empty);


                request.ParsedName       = parsed.Name;
                request.ParsedEmail      = parsed.Email;
                request.ParsedSkills     = (parsed.Skills != null && parsed.Skills.Count > 0)
                    ? string.Join(", ", parsed.Skills)
                    : null;
                request.ParsedExperience = parsed.Experience;
                request.ParsedEducation  = parsed.Education;
            }


            var letter = _generator.Generate(request);
            request.GeneratedLetter = letter;

            ViewBag.Resumes = await _db.Resumes
                .Where(r => r.ParsedByAi)
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            ViewBag.Jobs = await _db.Jobs
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return View("Preview", request);
        }
    }
}
