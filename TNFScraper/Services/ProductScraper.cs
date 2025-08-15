using System.Net;
using System.Text.Json;

namespace TNFScraper
{
    public sealed class ProductScraper : IDisposable
    {
        private readonly HttpClient _client;
        private bool _disposed;

        public ProductScraper()
        {
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = cookieContainer,
                UseCookies = true
            };
            _client = new HttpClient(handler);
            AddStaticHeaders(_client);
            AddCorrelationHeaders(_client);
        }

        public async Task<List<ProductMapped>?> ScrapeAsync(string productId, CancellationToken ct = default)
        {
            var productUrl = string.Format(Constants.ProductDetailsUrlTemplate, productId);
            var productPageUrl = string.Format(Constants.ProductPageUrlTemplate, productId);
            var ratingUrl = string.Format(Constants.ProductReviewsUrlTemplate, productId);

            var responseProduct = await _client.GetAsync(productUrl, ct);
            if (!responseProduct.IsSuccessStatusCode)
            {
                await LogError("product", responseProduct);
                return null;
            }
            var rawJsonProduct = await responseProduct.Content.ReadAsStringAsync(ct);

            await _client.GetAsync(productPageUrl, ct);
            _client.DefaultRequestHeaders.Remove("Referer");
            _client.DefaultRequestHeaders.Add("Referer", productPageUrl);

            var responseRating = await _client.GetAsync(ratingUrl, ct);
            if (!responseRating.IsSuccessStatusCode)
            {
                await LogError("rating", responseRating);
                return null;
            }
            var rawJsonRating = await responseRating.Content.ReadAsStringAsync(ct);

            return ProductMapper.MapAll(rawJsonProduct, rawJsonRating);
        }

        public static string Serialize(List<ProductMapped> mappedList)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Serialize(mappedList, options);
        }

        private static async Task LogError(string stage, HttpResponseMessage resp)
        {
            Console.WriteLine($"Error ({stage}): {resp.StatusCode}");
            var body = await resp.Content.ReadAsStringAsync();
            Console.WriteLine(body);
        }

        private static void AddStaticHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8,application/json");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
            client.DefaultRequestHeaders.Add("Brand", "TNF");
            client.DefaultRequestHeaders.Add("Channel", "ECOMM");
            client.DefaultRequestHeaders.Add("Locale", "en_US");
            client.DefaultRequestHeaders.Add("Region", "NORA");
            client.DefaultRequestHeaders.Add("Siteid", "TNF-US");
            client.DefaultRequestHeaders.Add("Source", "ECOM15");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:141.0) Gecko/20100101 Firefox/141.0");
        }

        private static void AddCorrelationHeaders(HttpClient client)
        {
            client.DefaultRequestHeaders.Remove("x-correlation-id");
            client.DefaultRequestHeaders.Remove("x-transaction-id");
            client.DefaultRequestHeaders.Add("x-correlation-id", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("x-transaction-id", Guid.NewGuid().ToString());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _client.Dispose();
            _disposed = true;
        }
    }
}