using LeadForgeAI.Models;
using System.Text;
using System.Text.Json;

namespace LeadForgeAI.Services
{
    public class GeminiEnrichmentService : IEnrichmentService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GeminiEnrichmentService> _logger;

        public GeminiEnrichmentService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GeminiEnrichmentService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiKey = configuration["GeminiApiKey"] ?? throw new InvalidOperationException("Gemini API Key not configured");
            _logger = logger;
        }

        public async Task<Lead> EnrichLeadAsync(string domain, int jobId)
        {
            try
            {
                var prompt = $@"Generate realistic B2B company information for the domain: {domain}

Please provide the following details in JSON format:
- companyName: The company name (infer from domain)
- industry: The primary industry (e.g., Technology, Healthcare, Finance, Manufacturing, etc.)
- employeeCount: Company size (e.g., 1-10, 11-50, 51-200, 201-500, 500+)
- businessEmail: A professional contact email (format: contact@domain or info@domain)
- phone: A realistic business phone number
- leadScore: Lead quality score from 1-10 based on company profile
- companyDescription: A 2-sentence description of what the company likely does
- country: The likely country of operation

Return ONLY valid JSON without any markdown formatting or code blocks.";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 500
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={_apiKey}",
                    content
                );

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Gemini API error: {response.StatusCode}");
                    return CreateFallbackLead(domain, jobId);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var geminiResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                var generatedText = geminiResponse
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "{}";

                // Clean up the response (remove markdown formatting if present)
                generatedText = generatedText.Trim();
                if (generatedText.StartsWith("```json"))
                {
                    generatedText = generatedText.Substring(7);
                }
                if (generatedText.StartsWith("```"))
                {
                    generatedText = generatedText.Substring(3);
                }
                if (generatedText.EndsWith("```"))
                {
                    generatedText = generatedText.Substring(0, generatedText.Length - 3);
                }
                generatedText = generatedText.Trim();

                var enrichedData = JsonSerializer.Deserialize<EnrichedData>(generatedText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (enrichedData == null)
                {
                    return CreateFallbackLead(domain, jobId);
                }

                return new Lead
                {
                    JobId = jobId,
                    Domain = domain,
                    CompanyName = enrichedData.CompanyName ?? domain,
                    Industry = enrichedData.Industry ?? "Unknown",
                    EmployeeCount = enrichedData.EmployeeCount ?? "Unknown",
                    BusinessEmail = enrichedData.BusinessEmail ?? $"contact@{domain}",
                    Phone = enrichedData.Phone ?? "N/A",
                    LeadScore = enrichedData.LeadScore,
                    CompanyDescription = enrichedData.CompanyDescription ?? "",
                    Country = enrichedData.Country ?? "Unknown",
                    EnrichedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error enriching lead for domain: {domain}");
                return CreateFallbackLead(domain, jobId);
            }
        }

        private Lead CreateFallbackLead(string domain, int jobId)
        {
            return new Lead
            {
                JobId = jobId,
                Domain = domain,
                CompanyName = domain.Split('.')[0],
                Industry = "Technology",
                EmployeeCount = "11-50",
                BusinessEmail = $"contact@{domain}",
                Phone = "+1-555-0100",
                LeadScore = 5,
                CompanyDescription = $"Company operating at {domain}",
                Country = "United States",
                EnrichedAt = DateTime.UtcNow
            };
        }

        private class EnrichedData
        {
            public string? CompanyName { get; set; }
            public string? Industry { get; set; }
            public string? EmployeeCount { get; set; }
            public string? BusinessEmail { get; set; }
            public string? Phone { get; set; }
            public int LeadScore { get; set; }
            public string? CompanyDescription { get; set; }
            public string? Country { get; set; }
        }
    }
}
