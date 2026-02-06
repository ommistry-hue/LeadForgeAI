using System.ComponentModel.DataAnnotations;

namespace LeadForgeAI.Models
{
    public class Lead
    {
        public int Id { get; set; }

        public int JobId { get; set; }

        [Required]
        public string Domain { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string Industry { get; set; } = string.Empty;

        public string EmployeeCount { get; set; } = string.Empty;

        public string BusinessEmail { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public int LeadScore { get; set; } = 0;

        public string CompanyDescription { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public DateTime EnrichedAt { get; set; } = DateTime.UtcNow;

        public virtual Job Job { get; set; } = null!;
    }
}
