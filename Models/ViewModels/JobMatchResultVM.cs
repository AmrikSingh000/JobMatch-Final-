using System.Collections.Generic;
using JobMatch.Models;

namespace JobMatch.Models.ViewModels
{
    public class JobMatchResultVM
    {
        public Job Job { get; set; } = null!;
        public int MatchScore { get; set; }
        public int MatchPercent { get; set; }
        public List<string> MatchedSkills { get; set; } = new List<string>();
    }
}
