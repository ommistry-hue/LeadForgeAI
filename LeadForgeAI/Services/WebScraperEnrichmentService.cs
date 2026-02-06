using LeadForgeAI.Models;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Net;

namespace LeadForgeAI.Services
{
    public class WebScraperEnrichmentService : IEnrichmentService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WebScraperEnrichmentService> _logger;

        // Common email patterns to look for
        private static readonly Regex EmailRegex = new Regex(
            @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        // Phone number patterns (international formats)
        private static readonly Regex PhoneRegex = new Regex(
            @"(\+?\d{1,3}[-.\s]?)?(\(?\d{3}\)?[-.\s]?)?\d{3}[-.\s]?\d{4}",
            RegexOptions.Compiled
        );

        // Industry keywords mapping
        private static readonly Dictionary<string, string[]> IndustryKeywords = new()
        {
            { "Technology", new[] { "software", "tech", "cloud", "saas", "app", "digital", "IT", "cyber", "data", "AI", "machine learning" } },
            { "Finance", new[] { "bank", "finance", "investment", "trading", "fintech", "payment", "insurance", "loan" } },
            { "Healthcare", new[] { "health", "medical", "hospital", "clinic", "pharma", "care", "patient", "doctor" } },
            { "E-commerce", new[] { "shop", "store", "retail", "ecommerce", "marketplace", "buy", "sell" } },
            { "Marketing", new[] { "marketing", "advertising", "agency", "brand", "campaign", "seo" } },
            { "Education", new[] { "education", "learning", "training", "course", "school", "university", "academy" } },
            { "Real Estate", new[] { "real estate", "property", "housing", "apartment", "commercial" } },
            { "Manufacturing", new[] { "manufacturing", "factory", "production", "industrial", "supply chain" } },
            { "Consulting", new[] { "consulting", "advisory", "strategy", "consulting", "professional services" } }
        };

        public WebScraperEnrichmentService(IHttpClientFactory httpClientFactory, ILogger<WebScraperEnrichmentService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _logger = logger;
        }

        public async Task<Lead> EnrichLeadAsync(string domain, int jobId)
        {
            try
            {
                _logger.LogInformation($"Starting enrichment for domain: {domain}");

                // Try to fetch the website
                var url = $"https://{domain}";
                var htmlContent = await FetchWebsiteAsync(url);

                if (string.IsNullOrEmpty(htmlContent))
                {
                    // Try without https
                    url = $"http://{domain}";
                    htmlContent = await FetchWebsiteAsync(url);
                }

                if (string.IsNullOrEmpty(htmlContent))
                {
                    _logger.LogWarning($"Could not fetch website for domain: {domain}");
                    return CreateFallbackLead(domain, jobId, "Website unreachable");
                }

                // Parse HTML
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlContent);

                // Extract data
                var companyName = ExtractCompanyName(htmlDoc, domain);
                var emails = ExtractEmails(htmlContent, domain);
                var phones = ExtractPhones(htmlContent);
                var industry = DetectIndustry(htmlContent, htmlDoc);
                var description = ExtractDescription(htmlDoc);
                var country = ExtractCountry(htmlContent);

                // Calculate lead score based on data completeness
                var leadScore = CalculateLeadScore(emails.Count, phones.Count, !string.IsNullOrEmpty(description));

                return new Lead
                {
                    JobId = jobId,
                    Domain = domain,
                    CompanyName = companyName,
                    Industry = industry,
                    EmployeeCount = "Unknown", // Can't reliably scrape this
                    BusinessEmail = emails.FirstOrDefault() ?? $"info@{domain}",
                    Phone = phones.FirstOrDefault() ?? "Not found",
                    LeadScore = leadScore,
                    CompanyDescription = description,
                    Country = country,
                    EnrichedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error enriching domain: {domain}");
                return CreateFallbackLead(domain, jobId, $"Error: {ex.Message}");
            }
        }

        private async Task<string> FetchWebsiteAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error fetching {url}: {ex.Message}");
            }
            return string.Empty;
        }

        private string ExtractCompanyName(HtmlDocument doc, string domain)
        {
            // Try meta tags first
            var ogSiteName = doc.DocumentNode.SelectSingleNode("//meta[@property='og:site_name']")?.GetAttributeValue("content", "");
            if (!string.IsNullOrEmpty(ogSiteName)) return ogSiteName;

            // Try title tag
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
            if (!string.IsNullOrEmpty(title))
            {
                // Clean up title (remove common suffixes)
                title = Regex.Replace(title, @"\s*[-|–]\s*(Home|Official Site|Welcome).*$", "", RegexOptions.IgnoreCase);
                if (!string.IsNullOrEmpty(title)) return title;
            }

            // Fallback: capitalize domain name
            var name = domain.Split('.')[0];
            return char.ToUpper(name[0]) + name.Substring(1);
        }

        private List<string> ExtractEmails(string htmlContent, string domain)
        {
            var emails = new HashSet<string>();
            var matches = EmailRegex.Matches(htmlContent);

            foreach (Match match in matches)
            {
                var email = match.Value.ToLower();

                // Filter out common false positives and prefer domain-specific emails
                if (!email.Contains("example.") &&
                    !email.Contains("@placeholder") &&
                    !email.Contains("@domain.") &&
                    !email.Contains(".png") &&
                    !email.Contains(".jpg"))
                {
                    // Prioritize emails from the same domain
                    if (email.Contains($"@{domain}") || email.Contains($"@{domain.Split('.')[0]}"))
                    {
                        emails.Add(email);
                    }
                    else if (emails.Count < 3) // Add other emails as fallback
                    {
                        emails.Add(email);
                    }
                }
            }

            return emails.Take(3).ToList();
        }

        private List<string> ExtractPhones(string htmlContent)
        {
            var phones = new HashSet<string>();
            var matches = PhoneRegex.Matches(htmlContent);

            foreach (Match match in matches)
            {
                var phone = match.Value.Trim();

                // Filter out dates and other false positives
                if (phone.Length >= 10 && phone.Length <= 20)
                {
                    phones.Add(phone);
                    if (phones.Count >= 2) break;
                }
            }

            return phones.ToList();
        }

        private string DetectIndustry(string htmlContent, HtmlDocument doc)
        {
            var contentLower = htmlContent.ToLower();

            // Try meta description first
            var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']")?.GetAttributeValue("content", "")?.ToLower() ?? "";

            var combinedText = $"{contentLower} {metaDesc}";

            // Score each industry
            var industryScores = new Dictionary<string, int>();
            foreach (var kvp in IndustryKeywords)
            {
                var score = kvp.Value.Count(keyword => combinedText.Contains(keyword.ToLower()));
                if (score > 0)
                {
                    industryScores[kvp.Key] = score;
                }
            }

            // Return industry with highest score
            if (industryScores.Any())
            {
                return industryScores.OrderByDescending(x => x.Value).First().Key;
            }

            return "Business Services";
        }

        private string ExtractDescription(HtmlDocument doc)
        {
            // Try meta description
            var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']")?.GetAttributeValue("content", "");
            if (!string.IsNullOrEmpty(metaDesc) && metaDesc.Length > 20)
            {
                return metaDesc.Length > 200 ? metaDesc.Substring(0, 200) + "..." : metaDesc;
            }

            // Try og:description
            var ogDesc = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']")?.GetAttributeValue("content", "");
            if (!string.IsNullOrEmpty(ogDesc) && ogDesc.Length > 20)
            {
                return ogDesc.Length > 200 ? ogDesc.Substring(0, 200) + "..." : ogDesc;
            }

            // Try first paragraph
            var firstP = doc.DocumentNode.SelectSingleNode("//p")?.InnerText?.Trim();
            if (!string.IsNullOrEmpty(firstP) && firstP.Length > 20)
            {
                return firstP.Length > 200 ? firstP.Substring(0, 200) + "..." : firstP;
            }

            return "No description available";
        }

        private string ExtractCountry(string htmlContent)
        {
            // Simple country detection from common patterns
            var countries = new[] { "United States", "USA", "United Kingdom", "UK", "Canada", "Australia", "India", "Germany", "France" };

            foreach (var country in countries)
            {
                if (htmlContent.Contains(country, StringComparison.OrdinalIgnoreCase))
                {
                    return country.Replace("USA", "United States").Replace("UK", "United Kingdom");
                }
            }

            return "Unknown";
        }

        private int CalculateLeadScore(int emailCount, int phoneCount, bool hasDescription)
        {
            var score = 5; // Base score

            // Add points for found data
            if (emailCount > 0) score += 2;
            if (emailCount > 1) score += 1;
            if (phoneCount > 0) score += 2;
            if (hasDescription) score += 1;

            return Math.Min(10, score);
        }

        private Lead CreateFallbackLead(string domain, int jobId, string reason)
        {
            return new Lead
            {
                JobId = jobId,
                Domain = domain,
                CompanyName = domain.Split('.')[0],
                Industry = "Unknown",
                EmployeeCount = "Unknown",
                BusinessEmail = $"info@{domain}",
                Phone = "Not found",
                LeadScore = 3,
                CompanyDescription = $"Could not scrape data: {reason}",
                Country = "Unknown",
                EnrichedAt = DateTime.UtcNow
            };
        }
    }
}
