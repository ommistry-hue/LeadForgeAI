using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using LeadForgeAI.Models;

namespace LeadForgeAI.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Job> Jobs { get; set; }
    public DbSet<Lead> Leads { get; set; }
    public DbSet<UserCredits> UserCredits { get; set; }
    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public DbSet<UserSubscription> UserSubscriptions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seed subscription plans
        modelBuilder.Entity<SubscriptionPlan>().HasData(
            new SubscriptionPlan
            {
                Id = 1,
                Name = "Free",
                Price = 0,
                LeadLimit = 10,
                Features = "10 leads per month,Basic search,Email & phone enrichment",
                StripePriceId = "",
                StripeProductId = "",
                IsActive = true
            },
            new SubscriptionPlan
            {
                Id = 2,
                Name = "Pro",
                Price = 29,
                LeadLimit = 500,
                Features = "500 leads per month,Advanced search,Email & phone enrichment,Priority support",
                StripePriceId = "price_1SuRCwSEwGBhjH9cdgTTwAtj",
                StripeProductId = "prod_TsBZFdwA7zau09",
                IsActive = true
            },
            new SubscriptionPlan
            {
                Id = 3,
                Name = "Enterprise",
                Price = 99,
                LeadLimit = 999999,
                Features = "Unlimited leads,Advanced search,Email & phone enrichment,Priority support,API access,Custom integrations",
                StripePriceId = "price_1SuRDCSEwGBhjH9cLhCYXQdr",
                StripeProductId = "prod_TsBah3O2lsIIHJ",
                IsActive = true
            }
        );
    }
}
