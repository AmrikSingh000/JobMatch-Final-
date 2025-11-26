using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JobMatch.Data;
using JobMatch.Models;
using JobMatch.Models.ViewModels;
using JobMatch.Services.Parsing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Controllers
{
    [Authorize(Roles = "Jobseeker,Admin")]
    // All of this is basically about job–candidate match listing.
    public class MatchesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IResumeParser _resumeParser;
        private readonly IResumeTextExtractor _textExtractor;
        private readonly IWebHostEnvironment _env;

        public MatchesController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IResumeParser resumeParser,
            IResumeTextExtractor textExtractor,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _resumeParser = resumeParser;
            _textExtractor = textExtractor;
            _env = env;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? q)
        {
            var userId = _userManager.GetUserId(User);

            // get latest non-deleted resume for this user (ignore ParsedByAi flag to be safe)
            var resume = await _context.Resumes
                .Where(r => r.OwnerUserId == userId && !r.IsDeleted)
                .OrderByDescending(r => r.ParsedAtUtc ?? r.UploadedAt)
                .FirstOrDefaultAsync();

            var jobsQuery = _context.Jobs.Where(j => j.IsActive);

            // no resume at all -> just show recent jobs like before
            if (resume == null)
            {
                var fallbackJobs = await ApplySearchFilter(jobsQuery, q)
                    .OrderByDescending(j => j.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                var fallbackVm = fallbackJobs
                    .Select(j => new JobMatchResultVM
                    {
                        Job = j,
                        MatchScore = 0,
                        MatchPercent = 0,
                        MatchedSkills = new List<string>()
                    })
                    .ToList();

                ViewBag.Query = q ?? "";
                ViewBag.ResumeUsed = false;
                ViewBag.SkillCount = 0;
                ViewBag.DebugMsg = "No resume found for this user.";

                return View(fallbackVm);
            }

            // make sure we have skills; if none stored, try to re-parse the file now
            if (string.IsNullOrWhiteSpace(resume.ParsedSkillsCsv))
            {
                try
                {
                    var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                    var relativePath = (resume.FilePath ?? "").TrimStart('/', '\\');
                    var fullPath = Path.Combine(webRoot, relativePath);

                    var text = _textExtractor.ExtractText(fullPath) ?? "";
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var parsed = _resumeParser.Parse(text);

                        resume.ParsedName = parsed.Name;
                        resume.ParsedSkillsCsv = (parsed.Skills != null && parsed.Skills.Count > 0)
                            ? string.Join(",", parsed.Skills)
                            : resume.ParsedSkillsCsv; // leave as-is if still empty
                        resume.ParsedExperience = parsed.Experience;
                        resume.ParsedEducation = parsed.Education;
                        resume.ParsedByAi = true;
                        resume.ParsedAtUtc = DateTime.UtcNow;
                        resume.ParsedEngine = parsed.Engine;
                        resume.ParsedModel = parsed.Model;

                        await _context.SaveChangesAsync();
                    }
                }
                catch
                {
                    // swallow errors; well fall back below
                }
            }

            // build skills list from ParsedSkillsCsv
            var skills = (resume.ParsedSkillsCsv ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 1)
                .Select(s => s.ToLower())
                .Distinct()
                .ToList();

            if (skills.Count == 0)
            {
                // still no skills: show recent jobs but tell the user we couldn't detect any
                var fallbackJobs = await ApplySearchFilter(jobsQuery, q)
                    .OrderByDescending(j => j.CreatedAt)
                    .Take(50)
                    .ToListAsync();

                var fallbackVm = fallbackJobs
                    .Select(j => new JobMatchResultVM
                    {
                        Job = j,
                        MatchScore = 0,
                        MatchPercent = 0,
                        MatchedSkills = new List<string>()
                    })
                    .ToList();

                ViewBag.Query = q ?? "";
                ViewBag.ResumeUsed = false;
                ViewBag.SkillCount = 0;
                ViewBag.DebugMsg = "Resume found, but no skills could be detected.";

                return View(fallbackVm);
            }

            // at this point we have a resume and a non-empty skills list
            var allJobs = await ApplySearchFilter(jobsQuery, q).ToListAsync();
            var results = new List<JobMatchResultVM>();

            foreach (var job in allJobs)
            {
                var title = (job.Title ?? "").ToLower();
                var desc = (job.Description ?? "").ToLower();
                var tags = (job.TagsCsv ?? "").ToLower();

                var matchedSkills = new List<string>();
                var score = 0;

                foreach (var skill in skills)
                {
                    var hit = false;

                    if (!string.IsNullOrEmpty(tags) && tags.Contains(skill))
                    {
                        score += 3;
                        hit = true;
                    }

                    if (!string.IsNullOrEmpty(title) && title.Contains(skill))
                    {
                        score += 2;
                        hit = true;
                    }

                    if (!string.IsNullOrEmpty(desc) && desc.Contains(skill))
                    {
                        score += 1;
                        hit = true;
                    }

                    if (hit && !matchedSkills.Contains(skill, StringComparer.OrdinalIgnoreCase))
                    {
                        matchedSkills.Add(skill);
                    }
                }

                if (score > 0 && matchedSkills.Count > 0)
                {
                    var matchPercent = (int)Math.Round(
                        100.0 * matchedSkills.Count / skills.Count,
                        MidpointRounding.AwayFromZero);

                    if (matchPercent > 100) matchPercent = 100;

                    results.Add(new JobMatchResultVM
                    {
                        Job = job,
                        MatchScore = score,
                        MatchPercent = matchPercent,
                        MatchedSkills = matchedSkills
                    });
                }
            }

            results = results
                .OrderByDescending(x => x.MatchScore)
                .ThenByDescending(x => x.Job.CreatedAt)
                .Take(50)
                .ToList();

            ViewBag.Query = q ?? "";
            ViewBag.ResumeUsed = true;
            ViewBag.SkillCount = skills.Count;
            ViewBag.DebugMsg = $"Using resume #{resume.Id} with {skills.Count} skills.";

            return View(results);
        }

        private IQueryable<Job> ApplySearchFilter(IQueryable<Job> query, string? q)
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var t = q.Trim();
                query = query.Where(j =>
                    EF.Functions.Like(j.Title, $"%{t}%") ||
                    EF.Functions.Like(j.TagsCsv, $"%{t}%") ||
                    EF.Functions.Like(j.Description, $"%{t}%"));
            }

            return query;
        }
    }
}
