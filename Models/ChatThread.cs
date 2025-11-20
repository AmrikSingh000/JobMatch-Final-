using JobMatch.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JobMatch.Models
{
    public class ChatThread
    {
        public int Id { get; set; }

        [Required]
        public string RecruiterId { get; set; }

        [Required]
        public string JobSeekerId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsClosed { get; set; }

        // Navigation
        public virtual IdentityUser Recruiter { get; set; }
        public virtual IdentityUser JobSeeker { get; set; }
        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
