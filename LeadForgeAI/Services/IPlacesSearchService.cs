namespace LeadForgeAI.Services
{
    public interface IPlacesSearchService
    {
        Task<List<BusinessSearchResult>> SearchBusinessesAsync(string query, string country, string state);
    }

    public class BusinessSearchResult
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public double? Rating { get; set; }
        public string PlaceId { get; set; } = string.Empty;
    }
}
