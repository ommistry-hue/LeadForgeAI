using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using LeadForgeAI.Models;
using LeadForgeAI.Data;
using LeadForgeAI.Services;
using Microsoft.EntityFrameworkCore;

namespace LeadForgeAI.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;

    public HomeController(ApplicationDbContext context, UserManager<IdentityUser> userManager, ISubscriptionService subscriptionService)
    {
        _context = context;
        _userManager = userManager;
        _subscriptionService = subscriptionService;
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = _userManager.GetUserId(User);

            // Get subscription-based credits
            var subscription = await _subscriptionService.GetUserSubscriptionAsync(userId);
            var availableCredits = await _subscriptionService.GetAvailableCreditsAsync(userId);

            var recentJobs = await _context.Jobs
                .Where(j => j.UserId == userId)
                .OrderByDescending(j => j.CreatedAt)
                .Take(5)
                .ToListAsync();

            var totalProcessed = await _context.Jobs
                .Where(j => j.UserId == userId && j.Status == "Completed")
                .SumAsync(j => j.ProcessedLeads);

            ViewBag.Credits = availableCredits;
            ViewBag.TotalProcessed = totalProcessed;
            ViewBag.RecentJobs = recentJobs;
            ViewBag.PlanName = subscription?.SubscriptionPlan?.Name ?? "Free";
            ViewBag.PlanLimit = subscription?.SubscriptionPlan?.LeadLimit ?? 10;
            ViewBag.LeadsUsedThisMonth = subscription?.LeadsUsedThisMonth ?? 0;
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
