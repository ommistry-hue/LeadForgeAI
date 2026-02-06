using System.ComponentModel.DataAnnotations;

namespace LeadForgeAI.Models
{
    public class Job
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public int TotalLeads { get; set; }

        public int ProcessedLeads { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public int CreditsUsed { get; set; }

        public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
    }
}
