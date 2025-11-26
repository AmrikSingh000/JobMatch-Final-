using JobMatch.Data;
using JobMatch.Models;
using JobMatch.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace JobMatch.Controllers
{
    [Authorize]
    // In short, this is mainly for recruiter–jobseeker chat threads.
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ChatController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var uid = _userManager.GetUserId(User)!;
            var threads = await _context.ChatThreads
                .Include(t => t.Recruiter)
                .Include(t => t.JobSeeker)
                .Where(t => t.RecruiterId == uid || t.JobSeekerId == uid)
                .OrderByDescending(t => t.Id)
                .ToListAsync();

            return View(threads);
        }

        // Recruiter-only initiation
        [HttpGet]
        [Authorize(Roles = "Recruiter")]
        public async Task<IActionResult> Start(string jobSeekerId)
        {
            if (string.IsNullOrWhiteSpace(jobSeekerId)) return BadRequest();

            var recruiterId = _userManager.GetUserId(User)!;

            var thread = await _context.ChatThreads
                .FirstOrDefaultAsync(t => t.RecruiterId == recruiterId && t.JobSeekerId == jobSeekerId);

            if (thread == null)
            {
                thread = new ChatThread
                {
                    RecruiterId = recruiterId,
                    JobSeekerId = jobSeekerId,
                    CreatedAt = DateTime.UtcNow,
                    IsClosed = false
                };
                _context.ChatThreads.Add(thread);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Thread), new { id = thread.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Thread(int id)
        {
            var uid = _userManager.GetUserId(User)!;

            var thread = await _context.ChatThreads
                .Include(t => t.Recruiter)
                .Include(t => t.JobSeeker)
                .Include(t => t.Messages).ThenInclude(m => m.Sender)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (thread == null) return NotFound();
            if (thread.RecruiterId != uid && thread.JobSeekerId != uid) return Forbid();

            var isRecruiter = uid == thread.RecruiterId;
            var otherName = isRecruiter ? thread.JobSeeker?.UserName : thread.Recruiter?.UserName;

            var vm = new ChatThreadViewModel
            {
                ThreadId = thread.Id,
                OtherPartyName = otherName ?? "(unknown)",
                IsRecruiter = isRecruiter,
                Messages = thread.Messages
                    .OrderBy(m => m.SentAt)
                    .Select(m => new ChatMessageItem
                    {
                        Id = m.Id,
                        SenderName = m.Sender?.UserName ?? "Unknown",
                        MessageText = m.MessageText,
                        SentAt = m.SentAt,
                        IsMine = m.SenderId == uid
                    })
                    .ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Thread(ChatThreadViewModel model)
        {
            model.NewMessageText = model.NewMessageText?.Trim();
            if (string.IsNullOrWhiteSpace(model.NewMessageText))
            {
                ModelState.AddModelError(nameof(model.NewMessageText), "Please enter a message.");
                return await Thread(model.ThreadId); // re-render with validation error
            }

            var uid = _userManager.GetUserId(User)!;

            var thread = await _context.ChatThreads.FirstOrDefaultAsync(t => t.Id == model.ThreadId);
            if (thread == null) return NotFound();
            if (thread.RecruiterId != uid && thread.JobSeekerId != uid) return Forbid();

            _context.ChatMessages.Add(new ChatMessage
            {
                ChatThreadId = model.ThreadId,
                SenderId = uid,
                MessageText = model.NewMessageText!,
                SentAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Thread), new { id = model.ThreadId });
        }
    }
}