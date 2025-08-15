using TNFScraper;

class Program
{
    static async Task Main(string[] args)
    {
        var productId = GetProductId(args);
        if (productId is null) return;

        using var scraper = new ProductScraper();
        var mappedList = await scraper.ScrapeAsync(productId);
        if (mappedList is null) return;

        var output = ProductScraper.Serialize(mappedList);

        const string outputFolder = "outputs";
        Directory.CreateDirectory(outputFolder);
        var filePath = Path.Combine(outputFolder, $"{productId}.json");
        await File.WriteAllTextAsync(filePath, output);

        Console.WriteLine($"Output saved to file: {filePath}");
    }

    private static string? GetProductId(string[] args)
    {
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            return args[0].Trim();

        Console.WriteLine("Usage: dotnet run <productId>");
        Console.Write("Enter productId now (press Enter to abort): ");
        var entered = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(entered))
        {
            Console.WriteLine("No productId provided. Exiting.");
            return null;
        }
        return entered.Trim();
    }
}