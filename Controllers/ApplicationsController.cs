using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using JobMatch.Data;
using JobMatch.Models;
using JobMatch.Services.Email;

namespace JobMatch.Controllers
{
    [Authorize]
    // This part mostly deals with {desc}.
    public class ApplicationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IAppEmailSender _email;
        private readonly IWebHostEnvironment _env; 

        public ApplicationsController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IAppEmailSender email,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _email = email;
            _env = env;
        }

        [HttpPost]
        [Authorize(Roles = "Jobseeker,Admin,Recruiter")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(int id, string? coverLetter)
        {
            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.IsActive);
            if (job == null) return NotFound();

            var userId = _userManager.GetUserId(User)!;

            var exists = await _context.JobApplications.AnyAsync(a => a.JobId == id && a.ApplicantUserId == userId);
            if (exists)
            {
                TempData["Msg"] = "You have already applied to this job.";
                return RedirectToAction("Details", "Jobs", new { id });
            }

            var app = new JobApplication
            {
                JobId = id,
                ApplicantUserId = userId,
                SubmittedAt = DateTime.UtcNow,
                Status = ApplicationStatus.Pending,
                CoverLetter = coverLetter
            };

            _context.JobApplications.Add(app);
            await _context.SaveChangesAsync();

            TempData["Msg"] = "Application submitted.";
            return RedirectToAction(nameof(My));
        }

        [HttpGet]
        [Authorize(Roles = "Jobseeker,Admin,Recruiter")]
        public async Task<IActionResult> My()
        {
            var userId = _userManager.GetUserId(User)!;

            var list = await _context.JobApplications
                .Include(a => a.Job)
                .Where(a => a.ApplicantUserId == userId)
                .OrderByDescending(a => a.SubmittedAt)
                .ToListAsync();

            return View(list);
        }

        [HttpGet]
        [Authorize(Roles = "Recruiter,Admin")]
        public async Task<IActionResult> ForJob(int id)
        {
            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == id);
            if (job == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User) ?? "";
            var isOwner = job.PostedByUserId == currentUserId;
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");
            if (!isOwner && !isAdmin) return Forbid();

            var apps = await _context.JobApplications
                .Where(a => a.JobId == id)
                .OrderByDescending(a => a.SubmittedAt)
                .ToListAsync();

            var userIds = apps.Select(a => a.ApplicantUserId).Distinct().ToList();
            var users = _context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionary(u => u.Id, u => u.Email ?? u.UserName);

            ViewBag.Job = job;
            ViewBag.UserEmails = users;
            return View(apps);
        }

        [HttpPost]
        [Authorize(Roles = "Recruiter,Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, ApplicationStatus status)
        {
            var app = await _context.JobApplications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (app == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User) ?? "";
            var isOwner = app.Job.PostedByUserId == currentUserId;
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");
            if (!isOwner && !isAdmin) return Forbid();

            app.Status = status;
            await _context.SaveChangesAsync();

            TempData["Msg"] = "Application status updated.";

            try
            {
                var applicant = await _userManager.FindByIdAsync(app.ApplicantUserId);
                if (!string.IsNullOrWhiteSpace(applicant?.Email))
                {
                    var subj = $"Your application for '{app.Job.Title}' is now {app.Status}";
                    var body = $"<p>Hi,</p><p>Your application for <strong>{app.Job.Title}</strong> at <strong>{app.Job.Organization}</strong> is now <strong>{app.Status}</strong>.</p>";
                    await _email.SendAsync(applicant.Email!, subj, body);
                }
            }
            catch {  }

            return RedirectToAction(nameof(ForJob), new { id = app.JobId });
        }

        [HttpGet]
        [Authorize(Roles = "Recruiter,Admin")]
        public async Task<IActionResult> DownloadResume(int jobId, string applicantUserId)
        {
            if (string.IsNullOrWhiteSpace(applicantUserId)) return BadRequest();

            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User) ?? "";
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");
            var isOwner = job.PostedByUserId == currentUserId;
            if (!isOwner && !isAdmin) return Forbid();

            var resume = await _context.Resumes
                .Where(r => r.OwnerUserId == applicantUserId && !r.IsDeleted)
                .OrderByDescending(r => r.ParsedAtUtc ?? r.UploadedAt)
                .FirstOrDefaultAsync();

            if (resume == null || string.IsNullOrWhiteSpace(resume.FilePath))
            {
                TempData["Error"] = "No resume file found.";
                return RedirectToAction(nameof(ForJob), new { id = jobId });
            }

            var relative = resume.FilePath.TrimStart('~').TrimStart('/');
            var physicalPath = Path.Combine(_env.WebRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(physicalPath))
            {
                TempData["Error"] = "Resume file missing on server.";
                return RedirectToAction(nameof(ForJob), new { id = jobId });
            }

            var fileName = string.IsNullOrWhiteSpace(resume.OriginalFileName)
                ? Path.GetFileName(physicalPath)
                : resume.OriginalFileName;
            var stream = System.IO.File.OpenRead(physicalPath);
            return File(stream, "application/octet-stream", fileName);
        }
    }
}
