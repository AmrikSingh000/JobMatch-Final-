using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JobMatch.Data;
using JobMatch.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Controllers
{
    [Authorize(Roles = "Recruiter,Admin")]
    public class CandidatesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public CandidatesController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var uid = _userManager.GetUserId(User) ?? "";
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            var jobs = await _context.Jobs
                .Where(j => j.IsActive && (isAdmin || j.PostedByUserId == uid))
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return View(jobs); 
        }

        [HttpGet]
        public async Task<IActionResult> SearchForJob(int jobId, string? q)
        {
            var uid = _userManager.GetUserId(User) ?? "";
            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");
            var isOwner = job.PostedByUserId == uid;
            if (!isOwner && !isAdmin) return Forbid();

            var title = (job.Title ?? "").ToLowerInvariant();
            var desc = (job.Description ?? "").ToLowerInvariant();
            var tags = (job.TagsCsv ?? "").ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var extra = q.ToLowerInvariant();
                title = $"{title} {extra}";
                desc = $"{desc} {extra}";
                tags = $"{tags} {extra}";
            }

            var resumes = await _context.Resumes
                .Where(r => !r.IsDeleted && r.OwnerUserId != null &&
                            (r.ParsedSkillsCsv != null || r.ParsedExperience != null || r.ParsedEducation != null))
                .OrderByDescending(r => r.ParsedAtUtc ?? r.UploadedAt)
                .Take(1000)
                .ToListAsync();

            var results = new List<CandidateMatchVM>();

            foreach (var r in resumes)
            {
                var skills = (r.ParsedSkillsCsv ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 1)
                    .Select(s => s.ToLowerInvariant())
                    .Distinct()
                    .ToList();

                if (skills.Count == 0) continue;

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
                        matchedSkills.Add(skill);
                }

                if (score > 0 && matchedSkills.Count > 0)
                {
                    var matchPercent = (int)Math.Round(
                        100.0 * matchedSkills.Count / skills.Count,
                        MidpointRounding.AwayFromZero);
                    if (matchPercent > 100) matchPercent = 100;

                    results.Add(new CandidateMatchVM
                    {
                        ResumeId = r.Id,
                        OwnerUserId = r.OwnerUserId!,
                        OriginalFileName = r.OriginalFileName ?? "resume",
                        Score = score,                 
                        MatchPercent = matchPercent,   
                        MatchedSkills = matchedSkills.Take(12).ToList()
                    });
                }
            }

            var userIds = results.Select(x => x.OwnerUserId).Distinct().ToList();
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => new { u.UserName, u.Email });

            foreach (var r in results)
            {
                if (users.TryGetValue(r.OwnerUserId, out var u))
                {
                    r.UserName = u.UserName ?? r.OwnerUserId;
                    r.Email = u.Email ?? "";
                }
            }

            var ordered = results
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.UserName)
                .ToList();

            ViewBag.Job = job;
            return View("SearchForJob", ordered); 
        }
    }

    public sealed class CandidateMatchVM
    {
        public int ResumeId { get; set; }
        public string OwnerUserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string OriginalFileName { get; set; } = "";
        public int Score { get; set; }            
        public int MatchPercent { get; set; }     
        public List<string> MatchedSkills { get; set; } = new();
    }
}