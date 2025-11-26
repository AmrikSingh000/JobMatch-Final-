using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JobMatch.Data;
using JobMatch.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JobMatch.Services.Parsing;

namespace JobMatch.Controllers
{
    [Authorize(Roles = "Jobseeker")]
    // All of this is basically about CV upload and soft delete handling.
    public class CvController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IResumeParser _resumeParser;
        private readonly IResumeTextExtractor _textExtractor;

        public CvController(
            ApplicationDbContext db,
            IWebHostEnvironment env,
            UserManager<IdentityUser> userManager,
            IResumeParser resumeParser,
            IResumeTextExtractor textExtractor)
        {
            _db = db;
            _env = env;
            _userManager = userManager;
            _resumeParser = resumeParser;
            _textExtractor = textExtractor;
        }

        [HttpGet]
        public async Task<IActionResult> Upload()
        {
            var userId = _userManager.GetUserId(User);
            var myResumes = await _db.Resumes
                .Where(r => r.OwnerUserId == userId && !r.IsDeleted)
                .OrderByDescending(r => r.UploadedAt)
                .ToListAsync();

            return View(myResumes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile? file)
        {
            if (file is null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file.";
                return RedirectToAction(nameof(Upload));
            }

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var uploadsRoot = Path.Combine(webRoot, "uploads");
            Directory.CreateDirectory(uploadsRoot);

            var originalName = Path.GetFileName(file.FileName) ?? "upload";
            if (string.IsNullOrWhiteSpace(originalName))
                originalName = "upload";

            var serverFileName = $"{Guid.NewGuid():N}_{originalName}";
            var fullPath = Path.Combine(uploadsRoot, serverFileName);

            await using (var fs = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(fs);
            }

            var userId = _userManager.GetUserId(User);

            var resume = new Resume
            {
                OriginalFileName = originalName,
                FilePath = $"/uploads/{serverFileName}",
                UploadedAt = DateTime.UtcNow,
                OwnerUserId = userId
            };

            // extract text from the resume (PDF/DOCX/TXT)
            var resumeText = _textExtractor.ExtractText(fullPath) ?? "";

            var parsed = _resumeParser.Parse(resumeText);

            // save structured parsed fields
            resume.ParsedName = parsed.Name;
            resume.ParsedSkillsCsv = (parsed.Skills != null && parsed.Skills.Count > 0)
                         ? string.Join(",", parsed.Skills)
                               : null;
            // fallback
            resume.ParsedExperience = parsed.Experience;
            resume.ParsedEducation = parsed.Education;
            resume.ParsedByAi = true;
            resume.ParsedAtUtc = DateTime.UtcNow;
            resume.ParsedEngine = parsed.Engine;
            resume.ParsedModel = parsed.Model;

            _db.Resumes.Add(resume);
            await _db.SaveChangesAsync();

            TempData["Msg"] = $"CV uploaded & parsed: {originalName}";

            // send user immediately to Matches page
            return RedirectToAction("Index", "Matches");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var resume = await _db.Resumes.FirstOrDefaultAsync(
                r => r.Id == id && r.OwnerUserId == userId && !r.IsDeleted
            );

            if (resume == null)
            {
                TempData["Error"] = "CV not found or already deleted.";
                return RedirectToAction(nameof(Upload));
            }

            resume.IsDeleted = true;
            resume.DeletedAtUtc = DateTime.UtcNow;
            resume.DeletedByUserId = userId;

            _db.Resumes.Update(resume);
            await _db.SaveChangesAsync();

            TempData["Msg"] = "CV deleted.";
            return RedirectToAction(nameof(Upload));
        }
    }
}
