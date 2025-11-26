using JobMatch.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JobMatch.Data
{
    // All of this is basically about EF Core database context wiring.
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Job> Jobs => Set<Job>();
        public DbSet<Resume> Resumes => Set<Resume>();
        public DbSet<JobApplication> JobApplications => Set<JobApplication>();
        public DbSet<MatchScore> MatchScores => Set<MatchScore>();
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<ChatThread> ChatThreads { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            //  avoid multiple cascade paths on AspNetUsers

            builder.Entity<ChatThread>()
                .HasOne(ct => ct.Recruiter)
                .WithMany()
                .HasForeignKey(ct => ct.RecruiterId)
                .OnDelete(DeleteBehavior.Restrict); // or DeleteBehavior.NoAction

            builder.Entity<ChatThread>()
                .HasOne(ct => ct.JobSeeker)
                .WithMany()
                .HasForeignKey(ct => ct.JobSeekerId)
                .OnDelete(DeleteBehavior.Restrict); // or DeleteBehavior.NoAction
        }
    }
}
