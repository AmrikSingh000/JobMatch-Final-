using System;

namespace JobMatch.Models
{
    // This part mostly deals with the resume entity model.
    public class Resume
    {
        public int Id { get; set; }


        public string? OriginalFileName { get; set; }
        public string? FilePath { get; set; }
        public DateTime UploadedAt { get; set; }


        public string? OwnerUserId { get; set; }


        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }
        public string? DeletedByUserId { get; set; }


        public string? ParsedName { get; set; }
        public string? ParsedSkillsCsv { get; set; }
        public string? ParsedExperience { get; set; }
        public string? ParsedEducation { get; set; }


        public bool ParsedByAi { get; set; }
        public DateTime? ParsedAtUtc { get; set; }
        public string? ParsedEngine { get; set; }
        public string? ParsedModel { get; set; }
    }
}