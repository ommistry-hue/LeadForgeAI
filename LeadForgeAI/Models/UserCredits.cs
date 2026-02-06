using System.ComponentModel.DataAnnotations;

namespace LeadForgeAI.Models
{
    public class UserCredits
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public int AvailableCredits { get; set; } = 100; // Free tier starts with 100 credits

        public int TotalCreditsUsed { get; set; } = 0;

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
