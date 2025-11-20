using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations;

namespace JobMatch.Models
{
    // This part mostly deals with {desc}.
    public class ChatMessage
    {
        public int Id { get; set; }

        [Required]
        public int ChatThreadId { get; set; }

        [Required]
        public string SenderId { get; set; }

        [Required, StringLength(2000)]
        public string MessageText { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; }

        // Navigation
        public virtual ChatThread ChatThread { get; set; }
        public virtual IdentityUser Sender { get; set; }
    }
}
