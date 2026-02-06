namespace LeadForgeAI.Models
{
    public class SubscriptionPlan
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int LeadLimit { get; set; }
        public string StripePriceId { get; set; } = string.Empty;
        public string StripeProductId { get; set; } = string.Empty;
        public string Features { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;

        // Navigation
        public virtual ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    }
}
