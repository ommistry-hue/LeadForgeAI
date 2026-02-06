using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LeadForgeAI.Data;
using LeadForgeAI.Models;
using Stripe;
using Stripe.Checkout;

namespace LeadForgeAI.Controllers
{
    [Authorize]
    public class SubscriptionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(
            ApplicationDbContext context,
            UserManager<IdentityUser> userManager,
            IConfiguration configuration,
            ILogger<SubscriptionController> logger)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;

            // Initialize Stripe
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            // Get current subscription
            var currentSubscription = await _context.UserSubscriptions
                .Include(s => s.SubscriptionPlan)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == "active");

            // Get all available plans
            var plans = await _context.SubscriptionPlans
                .Where(p => p.IsActive)
                .OrderBy(p => p.Price)
                .ToListAsync();

            ViewBag.CurrentSubscription = currentSubscription;
            ViewBag.Plans = plans;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateCheckoutSession(int planId)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);
            var plan = await _context.SubscriptionPlans.FindAsync(planId);

            if (plan == null || plan.Price == 0)
            {
                TempData["Error"] = "Invalid plan selected.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                Currency = "usd",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = $"LeadForgeAI {plan.Name} Plan",
                                    Description = plan.Features,
                                },
                                UnitAmount = (long)(plan.Price * 100), // Convert to cents
                                Recurring = new SessionLineItemPriceDataRecurringOptions
                                {
                                    Interval = "month",
                                },
                            },
                            Quantity = 1,
                        },
                    },
                    Mode = "subscription",
                    SuccessUrl = $"{Request.Scheme}://{Request.Host}/Subscription/Success?session_id={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = $"{Request.Scheme}://{Request.Host}/Subscription/Index",
                    CustomerEmail = user!.Email,
                    Metadata = new Dictionary<string, string>
                    {
                        { "user_id", userId! },
                        { "plan_id", planId.ToString() }
                    }
                };

                var service = new SessionService();
                var session = await service.CreateAsync(options);

                return Redirect(session.Url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Stripe checkout session");
                TempData["Error"] = "An error occurred while processing your request. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Success(string session_id)
        {
            try
            {
                var service = new SessionService();
                var session = await service.GetAsync(session_id);

                if (session.PaymentStatus == "paid")
                {
                    var userId = session.Metadata["user_id"];
                    var planId = int.Parse(session.Metadata["plan_id"]);

                    // Cancel any existing active subscriptions
                    var existingSubscriptions = await _context.UserSubscriptions
                        .Where(s => s.UserId == userId && s.Status == "active")
                        .ToListAsync();

                    foreach (var sub in existingSubscriptions)
                    {
                        sub.Status = "canceled";
                        sub.EndDate = DateTime.UtcNow;
                    }

                    // Create new subscription
                    var newSubscription = new UserSubscription
                    {
                        UserId = userId,
                        SubscriptionPlanId = planId,
                        StripeSubscriptionId = session.SubscriptionId!,
                        StripeCustomerId = session.CustomerId!,
                        Status = "active",
                        StartDate = DateTime.UtcNow,
                        LeadsUsedThisMonth = 0,
                        LastResetDate = DateTime.UtcNow
                    };

                    _context.UserSubscriptions.Add(newSubscription);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Subscription activated successfully!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing successful subscription");
                TempData["Error"] = "Payment received but there was an issue activating your subscription. Please contact support.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var stripeSignature = Request.Headers["Stripe-Signature"];

            try
            {
                var webhookSecret = _configuration["Stripe:WebhookSecret"];
                var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);

                // Handle different event types
                if (stripeEvent.Type == "customer.subscription.deleted")
                {
                    var subscription = stripeEvent.Data.Object as Stripe.Subscription;
                    var userSub = await _context.UserSubscriptions
                        .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription!.Id);

                    if (userSub != null)
                    {
                        userSub.Status = "canceled";
                        userSub.EndDate = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
                else if (stripeEvent.Type == "customer.subscription.updated")
                {
                    var subscription = stripeEvent.Data.Object as Stripe.Subscription;
                    var userSub = await _context.UserSubscriptions
                        .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscription!.Id);

                    if (userSub != null)
                    {
                        userSub.Status = subscription!.Status;
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook error");
                return BadRequest();
            }
        }
    }
}
