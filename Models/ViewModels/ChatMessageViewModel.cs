using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace JobMatch.Models.ViewModels
{
    public class ChatThreadViewModel
    {
        public int ThreadId { get; set; }

        public string OtherPartyName { get; set; }

        public bool IsRecruiter { get; set; }

        public List<ChatMessageItem> Messages { get; set; } = new();

        [Required, StringLength(2000)]
        public string NewMessageText { get; set; }
    }

    public class ChatMessageItem
    {
        public int Id { get; set; }
        public string SenderName { get; set; }
        public string MessageText { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsMine { get; set; }
    }
}
