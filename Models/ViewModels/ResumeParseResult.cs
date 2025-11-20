using System.Collections.Generic;

namespace JobMatch.Models.ViewModels
{
    public sealed class ResumeParseResult
    {
        public string? Name { get; set; }
        public string? Email { get; set; }            
        public List<string> Skills { get; set; } = new();
        public string? Experience { get; set; }
        public string? Education { get; set; }

        public bool UsedAi { get; set; }
        public string? Engine { get; set; }
        public string? Model { get; set; }
    }
}