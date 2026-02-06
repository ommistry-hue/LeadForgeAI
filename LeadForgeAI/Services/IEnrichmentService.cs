using LeadForgeAI.Models;

namespace LeadForgeAI.Services
{
    public interface IEnrichmentService
    {
        Task<Lead> EnrichLeadAsync(string domain, int jobId);
    }
}
