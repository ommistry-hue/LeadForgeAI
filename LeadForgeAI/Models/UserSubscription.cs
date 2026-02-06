namespace LeadForgeAI.Models
{
    public class UserSubscription
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int SubscriptionPlanId { get; set; }
        public string StripeSubscriptionId { get; set; } = string.Empty;
        public string StripeCustomerId { get; set; } = string.Empty;
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = "active"; // active, canceled, past_due
        public int LeadsUsedThisMonth { get; set; } = 0;
        public DateTime LastResetDate { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual SubscriptionPlan SubscriptionPlan { get; set; } = null!;
    }
}
