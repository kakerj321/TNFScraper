namespace TNFScraper
{
    public class ProductMapped
    {
        public string Sku { get; set; } = "";
        public string RootDomain { get; set; } = "";
        public string Name { get; set; } = "";
        public string Brand { get; set; } = "";
        public string Currency { get; set; } = "";
        public double Price { get; set; } = 0.0;
        public List<string> ImageUrls { get; set; } = new();
        public Dictionary<string, StarRating> StarRatingDistribution { get; set; } = new();
        public string Category { get; set; } = "";
        public string CategoryLvl1 { get; set; } = "";
        public string CategoryLvl2 { get; set; } = "";
        public string CategoryLvl3 { get; set; } = "";
        public string CategoryLvl4 { get; set; } = "";
        public string Date { get; set; } = "";
        public string Time { get; set; } = "";
        public string Url { get; set; } = "";
        public string Mpn { get; set; } = "";
        public string Availability { get; set; } = "";
        public double AverageCustomerReview { get; set; } = 0.0;
        public double NumberOfCustomerReviews { get; set; } = 0.0;
        public Dictionary<string, string> Attributes { get; set; } = new();
        public string Description { get; set; } = "";
        public string Features { get; set; } = "";
        public List<string> Variants { get; set; } = new();
        public Dictionary<string, string> VariantAttributes { get; set; } = new(); 
        public List<Dictionary<string, Dictionary<string, string>>> VariantsAttributes { get; set; } = new();
    }

    public class StarRating
    {
        public int Number { get; set; } = 0;
        public double Percentage { get; set; } = 0.0;
    }
}