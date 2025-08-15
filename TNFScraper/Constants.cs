namespace TNFScraper
{
    public static class Constants
    {
        public const string BaseUrl = "https://www.thenorthface.com";
        public const string ProductDetailsUrlTemplate = BaseUrl + "/api/products/v2/products/{0}/details?locale=en-us";
        public const string ProductReviewsUrlTemplate = BaseUrl + "/api/products/v1/products/{0}/reviews?paging.from=0&paging.size=6&sort=Newest&getAll=false&locale=en-us&filters=rating%3A5";
        public const string ProductPageUrlTemplate = BaseUrl + "/en-us/p/-{0}";
    }
}