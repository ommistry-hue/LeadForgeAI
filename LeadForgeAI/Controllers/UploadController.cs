using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using LeadForgeAI.Data;
using LeadForgeAI.Models;
using LeadForgeAI.Services;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace LeadForgeAI.Controllers
{
    [Authorize]
    public class UploadController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEnrichmentService _enrichmentService;
        private readonly IPlacesSearchService _placesSearchService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<UploadController> _logger;

        public UploadController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IEnrichmentService enrichmentService,
            IPlacesSearchService placesSearchService,
            ISubscriptionService subscriptionService,
            ILogger<UploadController> logger)
        {
            _context = context;
            _userManager = userManager;
            _enrichmentService = enrichmentService;
            _placesSearchService = placesSearchService;
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            // Get subscription-based credits
            var availableCredits = await _subscriptionService.GetAvailableCreditsAsync(userId!);
            var subscription = await _subscriptionService.GetUserSubscriptionAsync(userId!);

            ViewBag.AvailableCredits = availableCredits;
            ViewBag.PlanName = subscription?.SubscriptionPlan?.Name ?? "Free";
            ViewBag.PlanLimit = subscription?.SubscriptionPlan?.LeadLimit ?? 10;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a valid CSV file.";
                return RedirectToAction(nameof(Index));
            }

            var userId = _userManager.GetUserId(User);

            // Reset monthly credits if needed FIRST
            await _subscriptionService.ResetMonthlyCreditsIfNeededAsync(userId!);

            var availableCredits = await _subscriptionService.GetAvailableCreditsAsync(userId!);
            var subscription = await _subscriptionService.GetUserSubscriptionAsync(userId!);
            var planLimit = subscription?.SubscriptionPlan?.LeadLimit ?? 10;

            if (availableCredits <= 0)
            {
                var planName = subscription?.SubscriptionPlan?.Name ?? "Free";
                TempData["Error"] = $"❌ You've used all {planLimit} credits for this month ({planName} plan). Upgrade or wait for reset!";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var domains = new List<string>();

                using (var reader = new StreamReader(file.OpenReadStream()))
                using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    HeaderValidated = null,
                    MissingFieldFound = null
                }))
                {
                    csv.Read();
                    csv.ReadHeader();

                    while (csv.Read())
                    {
                        // Try to get domain from various possible column names
                        var domain = csv.GetField("domain")
                                  ?? csv.GetField("Domain")
                                  ?? csv.GetField("website")
                                  ?? csv.GetField("Website")
                                  ?? csv.GetField(0);

                        if (!string.IsNullOrWhiteSpace(domain))
                        {
                            // Clean domain (remove http/https, www)
                            domain = domain.Replace("http://", "").Replace("https://", "").Replace("www.", "").Trim().Split('/')[0];
                            domains.Add(domain);
                        }
                    }
                }

                if (domains.Count == 0)
                {
                    TempData["Error"] = "No valid domains found in CSV. Ensure your CSV has a 'domain' or 'website' column.";
                    return RedirectToAction(nameof(Index));
                }

                // STRICT LIMIT: Only process what's available RIGHT NOW
                var domainsToProcess = domains.Take(availableCredits).ToList();

                if (domainsToProcess.Count < domains.Count)
                {
                    TempData["Warning"] = $"⚠️ CSV has {domains.Count} domains but you only have {availableCredits} credits remaining. Processing {availableCredits} leads only.";
                }

                // Create job
                var job = new Job
                {
                    UserId = userId!,
                    FileName = file.FileName,
                    TotalLeads = domainsToProcess.Count,
                    ProcessedLeads = 0,
                    Status = "Processing",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Jobs.Add(job);
                await _context.SaveChangesAsync();

                // Verify we can process these leads
                var canProcess = await _subscriptionService.CanProcessLeadsAsync(userId!, domainsToProcess.Count);
                if (!canProcess)
                {
                    TempData["Error"] = $"❌ Cannot process {domainsToProcess.Count} leads. You only have {availableCredits} credits remaining!";
                    return RedirectToAction(nameof(Index));
                }

                // Process leads synchronously (for MVP - in production use background jobs)
                var creditsUsed = 0;
                foreach (var domain in domainsToProcess)
                {
                    // Double-check we still have credits (safety check)
                    var currentAvailable = await _subscriptionService.GetAvailableCreditsAsync(userId!);
                    if (currentAvailable <= 0)
                    {
                        _logger.LogWarning("User {UserId} ran out of credits mid-processing. Stopping at {Count} leads.", userId, creditsUsed);
                        break;
                    }

                    try
                    {
                        var lead = await _enrichmentService.EnrichLeadAsync(domain, job.Id);
                        _context.Leads.Add(lead);
                        job.ProcessedLeads++;
                        creditsUsed++;

                        // Deduct credits IMMEDIATELY after each lead (prevents race conditions)
                        await _subscriptionService.DeductCreditsAsync(userId!, 1);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing domain: {domain}");
                    }
                }

                // Update job status
                job.Status = "Completed";
                job.CompletedAt = DateTime.UtcNow;
                job.CreditsUsed = creditsUsed;

                await _context.SaveChangesAsync();

                var remainingCredits = await _subscriptionService.GetAvailableCreditsAsync(userId!);
                TempData["Success"] = $"✅ Successfully processed {creditsUsed} leads! {remainingCredits} credits remaining this month.";
                return RedirectToAction("ViewJob", "Leads", new { id = job.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV upload");
                TempData["Error"] = "An error occurred while processing your file. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public IActionResult Search()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SearchLeads(string query, string country, string state)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(country) || string.IsNullOrWhiteSpace(state))
            {
                TempData["Error"] = "Please fill in all search fields.";
                return RedirectToAction(nameof(Search));
            }

            var userId = _userManager.GetUserId(User);

            // Reset monthly credits if needed FIRST
            await _subscriptionService.ResetMonthlyCreditsIfNeededAsync(userId!);

            // Check subscription limits using service
            var availableCredits = await _subscriptionService.GetAvailableCreditsAsync(userId!);
            var subscription = await _subscriptionService.GetUserSubscriptionAsync(userId!);
            var planLimit = subscription?.SubscriptionPlan?.LeadLimit ?? 10;

            if (availableCredits <= 0)
            {
                var planName = subscription?.SubscriptionPlan?.Name ?? "Free";
                TempData["Error"] = $"❌ You've used all {planLimit} credits this month ({planName} plan). Upgrade or wait for reset!";
                return RedirectToAction(nameof(Search));
            }

            try
            {
                // Search for businesses using Google Places
                var businesses = await _placesSearchService.SearchBusinessesAsync(query, country, state);

                if (businesses.Count == 0)
                {
                    TempData["Warning"] = "No businesses found matching your search criteria.";
                    return RedirectToAction(nameof(Search));
                }

                // Limit results based on available credits (max 20 per search)
                var maxLeads = Math.Min(availableCredits, 20);
                var businessesToProcess = businesses.Take(maxLeads).ToList();

                if (businesses.Count > maxLeads)
                {
                    TempData["Warning"] = $"⚠️ Found {businesses.Count} businesses but processing only {maxLeads} (you have {availableCredits} credits remaining).";
                }

                // Create job
                var job = new Job
                {
                    UserId = userId!,
                    FileName = $"Search: {query} in {state}, {country}",
                    TotalLeads = businessesToProcess.Count,
                    ProcessedLeads = 0,
                    Status = "Processing",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Jobs.Add(job);
                await _context.SaveChangesAsync();

                // Verify we can process these leads
                var canProcess = await _subscriptionService.CanProcessLeadsAsync(userId!, businessesToProcess.Count);
                if (!canProcess)
                {
                    TempData["Error"] = $"❌ Cannot process {businessesToProcess.Count} leads. You only have {availableCredits} credits remaining!";
                    return RedirectToAction(nameof(Search));
                }

                // Process each business - enrich with website scraping
                var leadsProcessed = 0;
                foreach (var business in businessesToProcess)
                {
                    // Double-check we still have credits (safety check)
                    var currentAvailable = await _subscriptionService.GetAvailableCreditsAsync(userId!);
                    if (currentAvailable <= 0)
                    {
                        _logger.LogWarning("User {UserId} ran out of credits mid-search. Stopping at {Count} leads.", userId, leadsProcessed);
                        break;
                    }

                    try
                    {
                        Lead lead;

                        if (!string.IsNullOrEmpty(business.Website))
                        {
                            // Enrich using website
                            lead = await _enrichmentService.EnrichLeadAsync(business.Website, job.Id);
                        }
                        else
                        {
                            // Create lead from Places data only
                            lead = new Lead
                            {
                                JobId = job.Id,
                                Domain = business.Website ?? "N/A",
                                CompanyName = business.Name,
                                Industry = "Unknown",
                                EmployeeCount = "Unknown",
                                BusinessEmail = $"info@{business.Name.Replace(" ", "").ToLower()}.com",
                                Phone = business.Phone ?? "Not found",
                                LeadScore = business.Rating.HasValue ? (int)(business.Rating.Value * 2) : 5,
                                CompanyDescription = $"Found via search: {query}",
                                Country = country,
                                EnrichedAt = DateTime.UtcNow
                            };
                        }

                        _context.Leads.Add(lead);
                        job.ProcessedLeads++;
                        leadsProcessed++;

                        // Deduct credits IMMEDIATELY after each lead
                        await _subscriptionService.DeductCreditsAsync(userId!, 1);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing business: {business.Name}");
                    }
                }

                // Update job status
                job.Status = "Completed";
                job.CompletedAt = DateTime.UtcNow;
                job.CreditsUsed = leadsProcessed;

                await _context.SaveChangesAsync();

                var remainingCredits = await _subscriptionService.GetAvailableCreditsAsync(userId!);
                TempData["Success"] = $"✅ Successfully generated {leadsProcessed} leads! {remainingCredits} credits remaining this month.";
                return RedirectToAction("ViewJob", "Leads", new { id = job.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing lead search");
                TempData["Error"] = "An error occurred while searching for leads. Please try again.";
                return RedirectToAction(nameof(Search));
            }
        }
    }
}
