using LeadForgeAI.Data;
using LeadForgeAI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeadForgeAI.Services
{
    public interface ISubscriptionService
    {
        Task<UserSubscription?> GetUserSubscriptionAsync(string userId);
        Task<bool> CanProcessLeadsAsync(string userId, int requestedLeads);
        Task<int> GetAvailableCreditsAsync(string userId);
        Task DeductCreditsAsync(string userId, int leadsProcessed);
        Task ResetMonthlyCreditsIfNeededAsync(string userId);
    }

    public class SubscriptionService : ISubscriptionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(ApplicationDbContext context, ILogger<SubscriptionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<UserSubscription?> GetUserSubscriptionAsync(string userId)
        {
            return await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == "active");
        }

        public async Task<bool> CanProcessLeadsAsync(string userId, int requestedLeads)
        {
            var subscription = await GetUserSubscriptionAsync(userId);

            if (subscription == null)
            {
                // No subscription - create free plan
                await CreateFreeSubscriptionAsync(userId);
                subscription = await GetUserSubscriptionAsync(userId);
            }

            if (subscription == null) return false;

            // Reset monthly credits if needed
            await ResetMonthlyCreditsIfNeededAsync(userId);

            var plan = subscription.SubscriptionPlan;
            var availableCredits = plan.LeadLimit - subscription.LeadsUsedThisMonth;

            return availableCredits >= requestedLeads;
        }

        public async Task<int> GetAvailableCreditsAsync(string userId)
        {
            var subscription = await GetUserSubscriptionAsync(userId);

            if (subscription == null)
            {
                // No subscription - return free plan limit
                return 10;
            }

            await ResetMonthlyCreditsIfNeededAsync(userId);

            var plan = subscription.SubscriptionPlan;
            return plan.LeadLimit - subscription.LeadsUsedThisMonth;
        }

        public async Task DeductCreditsAsync(string userId, int leadsProcessed)
        {
            var subscription = await GetUserSubscriptionAsync(userId);

            if (subscription == null)
            {
                _logger.LogWarning("Attempted to deduct credits for user {UserId} with no subscription", userId);
                return;
            }

            subscription.LeadsUsedThisMonth += leadsProcessed;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deducted {Leads} credits for user {UserId}. Total used: {Total}/{Limit}",
                leadsProcessed, userId, subscription.LeadsUsedThisMonth, subscription.SubscriptionPlan.LeadLimit);
        }

        public async Task ResetMonthlyCreditsIfNeededAsync(string userId)
        {
            var subscription = await GetUserSubscriptionAsync(userId);

            if (subscription == null) return;

            // Check if we need to reset (30 days have passed)
            var daysSinceReset = (DateTime.UtcNow - subscription.LastResetDate).Days;

            if (daysSinceReset >= 30)
            {
                subscription.LeadsUsedThisMonth = 0;
                subscription.LastResetDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Reset monthly credits for user {UserId}. Plan: {Plan}",
                    userId, subscription.SubscriptionPlan.Name);
            }
        }

        private async Task CreateFreeSubscriptionAsync(string userId)
        {
            var freePlan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.Name == "Free");

            if (freePlan == null)
            {
                _logger.LogError("Free plan not found in database!");
                return;
            }

            var subscription = new UserSubscription
            {
                UserId = userId,
                SubscriptionPlanId = freePlan.Id,
                Status = "active",
                StartDate = DateTime.UtcNow,
                LastResetDate = DateTime.UtcNow,
                LeadsUsedThisMonth = 0
            };

            _context.UserSubscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created free subscription for user {UserId}", userId);
        }
    }
}
