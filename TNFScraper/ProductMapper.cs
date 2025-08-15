using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace TNFScraper
{
    public static class ProductMapper
    {
        public static ProductMapped Map(string rawJsonProduct, string rawJsonRating)
            => MapAll(rawJsonProduct, rawJsonRating).FirstOrDefault() ?? new ProductMapped();

        public static List<ProductMapped> MapAll(string rawJsonProduct, string rawJsonRating)
        {
            using var docProduct = JsonDocument.Parse(rawJsonProduct);
            using var docRating = JsonDocument.Parse(rawJsonRating);
            var rootProduct = docProduct.RootElement;
            var rootRating = docRating.RootElement;

            var baseId = GetString(rootProduct, "id");
            var baseMpn = GetMpn(rootProduct);
            var ratingDist = BuildStarRatingDistribution(rootRating);
            var avgRating = GetAverageRating(rootRating);
            var reviewCount = GetReviewCount(rootRating);
            var imageUrls = GetImageUrls(rootProduct);
            var price = GetPrice(rootProduct);
            var brand = GetBrand(rootProduct);
            var currency = GetString(rootProduct, "currency");
            var date = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss");
            var time = DateTime.Now.ToString("HH:mm:ss.ffffff");
            var url = GetCanonicalUrl(rootProduct);

            var globalAttributes = new Dictionary<string, string>();
            if (rootProduct.TryGetProperty("attributes", out var attributesEl) && attributesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var attr in attributesEl.EnumerateArray())
                {
                    if (!TryGetString(attr, "label", out var key) || string.IsNullOrWhiteSpace(key))
                        continue;
                    if (attr.TryGetProperty("options", out var optionsEl) && optionsEl.ValueKind == JsonValueKind.Array)
                    {
                        var values = new List<string>();
                        foreach (var option in optionsEl.EnumerateArray())
                            if (TryGetString(option, "label", out var val) && !string.IsNullOrWhiteSpace(val))
                                values.Add(val.Trim());
                        if (values.Count > 0)
                            globalAttributes[key.Trim()] = string.Join(", ", values);
                    }
                }
            }

            if (rootProduct.TryGetProperty("badge", out var badgeEl) && badgeEl.ValueKind == JsonValueKind.Object)
                if (TryGetString(badgeEl, "label", out var badge) && !string.IsNullOrWhiteSpace(badge))
                    globalAttributes["badge"] = badge;

            var (cat, c1, c2, c3, c4) = BuildCategories(rootProduct);

            var featuresList = ExtractFeatures(rootProduct);
            var featuresStr = JsonSerializer.Serialize(featuresList);
            var description = ExtractDescription(rootProduct);

            var attributeValueMap = BuildAttributeValueMap(rootProduct);

            var variantEntries = new List<(string sku, Dictionary<string, string> raw, Dictionary<string, string> display, string inventoryState)>();
            if (rootProduct.TryGetProperty("variants", out var variantsEl) && variantsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var variant in variantsEl.EnumerateArray())
                {
                    if (!variant.TryGetProperty("attributes", out var attrsEl) || attrsEl.ValueKind != JsonValueKind.Object)
                        continue;

                    var rawDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var displayDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var prop in attrsEl.EnumerateObject())
                    {
                        var rawValue = prop.Value.ValueKind == JsonValueKind.String
                            ? (prop.Value.GetString()?.Trim() ?? "")
                            : "";
                        var displayValue = rawValue;
                        if (attributeValueMap.TryGetValue(prop.Name, out var map) && map.TryGetValue(rawValue, out var friendly))
                            displayValue = friendly;

                        var keyName = prop.Name switch
                        {
                            "color" => "Color",
                            "size" => "Size",
                            "fitType" => "FitType",
                            _ => prop.Name
                        };

                        rawDict[keyName] = rawValue;
                        displayDict[keyName] = displayValue;
                    }

                    if (!string.IsNullOrEmpty(baseId))
                    {
                        var sku = BuildVariantUrl(baseId, rawDict.ToDictionary(k => k.Key, v => v.Value));
                        var state = variant.TryGetProperty("productInventoryState", out var stEl) && stEl.ValueKind == JsonValueKind.String
                            ? stEl.GetString() ?? ""
                            : "";
                        variantEntries.Add((sku, rawDict, displayDict, state));
                    }
                }
            }

            var allSkus = variantEntries.Select(v => v.sku).ToList();

            var variantsAttributesList = variantEntries
                .Select(v => new Dictionary<string, Dictionary<string, string>>
                {
                    { v.sku, v.display }
                })
                .ToList();

            var result = new List<ProductMapped>(variantEntries.Count);
            foreach (var (sku, raw, display, state) in variantEntries)
            {
                var pm = new ProductMapped
                {
                    Sku = sku,
                    RootDomain = "thenorthface.com",
                    Name = GetString(rootProduct, "name"),
                    Brand = brand,
                    Currency = currency,
                    Price = price,
                    ImageUrls = new List<string>(imageUrls),
                    StarRatingDistribution = CloneStarRating(ratingDist),
                    Category = cat,
                    CategoryLvl1 = c1,
                    CategoryLvl2 = c2,
                    CategoryLvl3 = c3,
                    CategoryLvl4 = c4,
                    Date = date,
                    Time = time,
                    Url = url,
                    Mpn = BuildVariantMpn(baseMpn, raw),
                    Availability = string.Equals(state, "InStock", StringComparison.OrdinalIgnoreCase) ? "InStock" : "OutOfStock",
                    AverageCustomerReview = avgRating,
                    NumberOfCustomerReviews = reviewCount,
                    Attributes = new Dictionary<string, string>(globalAttributes, StringComparer.OrdinalIgnoreCase),
                    Description = description,
                    Features = featuresStr,
                    Variants = allSkus,
                    VariantAttributes = new Dictionary<string, string>(display, StringComparer.OrdinalIgnoreCase),
                    VariantsAttributes = variantsAttributesList
                };

                result.Add(pm);
            }

            if (result.Count == 0)
            {
                var fallback = new ProductMapped
                {
                    Sku = baseId,
                    RootDomain = "thenorthface.com",
                    Name = GetString(rootProduct, "name"),
                    Brand = brand,
                    Currency = currency,
                    Price = price,
                    ImageUrls = imageUrls,
                    StarRatingDistribution = ratingDist,
                    Category = cat,
                    CategoryLvl1 = c1,
                    CategoryLvl2 = c2,
                    CategoryLvl3 = c3,
                    CategoryLvl4 = c4,
                    Date = date,
                    Time = time,
                    Url = url,
                    Mpn = baseMpn,
                    Availability = DetermineAvailability(rootProduct),
                    AverageCustomerReview = avgRating,
                    NumberOfCustomerReviews = reviewCount,
                    Attributes = globalAttributes,
                    Description = description,
                    Features = featuresStr,
                    Variants = new List<string>(),
                    VariantAttributes = new Dictionary<string, string>(),
                    VariantsAttributes = new List<Dictionary<string, Dictionary<string, string>>>()
                };
                result.Add(fallback);
            }

            return result;
        }

        private static string BuildVariantMpn(string baseMpn, Dictionary<string, string> raw)
        {
            if (string.IsNullOrEmpty(baseMpn)) return baseMpn;
            if (raw.TryGetValue("Color", out var color) && !string.IsNullOrEmpty(color))
            {
                return baseMpn.EndsWith(color, StringComparison.OrdinalIgnoreCase) ? baseMpn : baseMpn + color;
            }
            return baseMpn;
        }

        private static (string category, string c1, string c2, string c3, string c4) BuildCategories(JsonElement rootProduct)
        {
            var cats = new List<string>();
            if (rootProduct.TryGetProperty("breadcrumbs", out var bcEl) && bcEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var crumb in bcEl.EnumerateArray())
                    if (TryGetString(crumb, "label", out var lbl) && !string.IsNullOrWhiteSpace(lbl))
                        cats.Add(lbl.Trim());
            }
            if (cats.Count == 0) cats.Add("Home");
            else if (!string.Equals(cats[0], "Home", StringComparison.OrdinalIgnoreCase))
                cats.Insert(0, "Home");
            else
                cats[0] = "Home";

            return (
                string.Join(">", cats),
                cats.ElementAtOrDefault(0) ?? "",
                cats.ElementAtOrDefault(1) ?? "",
                cats.ElementAtOrDefault(2) ?? "",
                cats.ElementAtOrDefault(3) ?? ""
            );
        }

        private static Dictionary<string, StarRating> CloneStarRating(Dictionary<string, StarRating> src)
        {
            var dict = new Dictionary<string, StarRating>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in src)
                dict[kv.Key] = new StarRating { Number = kv.Value.Number, Percentage = kv.Value.Percentage };
            return dict;
        }

        // Helpers
        private static bool TryGetString(JsonElement element, string property, out string? value)
        {
            value = null;
            if (!element.TryGetProperty(property, out var prop) || prop.ValueKind != JsonValueKind.String)
                return false;
            value = prop.GetString();
            return true;
        }

        private static string GetString(JsonElement element, string property)
            => TryGetString(element, property, out var v) && v is not null ? v : "";

        private static double GetPrice(JsonElement rootProduct)
        {
            if (rootProduct.TryGetProperty("offers", out var offers) &&
                offers.ValueKind == JsonValueKind.Object &&
                offers.TryGetProperty("price", out var priceEl) &&
                priceEl.ValueKind == JsonValueKind.Number)
            {
                return priceEl.GetDouble();
            }
            return 0d;
        }

        private static string GetBrand(JsonElement rootProduct)
        {
            if (rootProduct.TryGetProperty("offers", out var offers) &&
                offers.ValueKind == JsonValueKind.Object &&
                offers.TryGetProperty("seller", out var seller) &&
                seller.ValueKind == JsonValueKind.Object &&
                seller.TryGetProperty("name", out var nameEl) &&
                nameEl.ValueKind == JsonValueKind.String)
            {
                return nameEl.GetString() ?? "";
            }
            return "";
        }

        private static string GetMpn(JsonElement rootProduct)
        {
            if (rootProduct.TryGetProperty("productJsonLd", out var jsonLd) &&
                jsonLd.ValueKind == JsonValueKind.Object &&
                jsonLd.TryGetProperty("mpn", out var mpnEl) &&
                mpnEl.ValueKind == JsonValueKind.String)
            {
                return mpnEl.GetString() ?? "";
            }
            return "";
        }

        private static string GetCanonicalUrl(JsonElement rootProduct)
        {
            var part = GetString(rootProduct, "id");
            return "https://www.thenorthface.com/en-us/p/-" + part;
        }

        private static double GetAverageRating(JsonElement rootRating)
        {
            if (rootRating.TryGetProperty("results", out var results) &&
                results.ValueKind == JsonValueKind.Array &&
                results.GetArrayLength() > 0 &&
                results[0].TryGetProperty("rollup", out var rollup) &&
                rollup.ValueKind == JsonValueKind.Object &&
                rollup.TryGetProperty("averageRating", out var avg) &&
                avg.ValueKind == JsonValueKind.Number)
            {
                return avg.GetDouble();
            }
            return 0d;
        }

        private static int GetReviewCount(JsonElement rootRating)
        {
            if (rootRating.TryGetProperty("results", out var results) &&
                results.ValueKind == JsonValueKind.Array &&
                results.GetArrayLength() > 0 &&
                results[0].TryGetProperty("rollup", out var rollup) &&
                rollup.ValueKind == JsonValueKind.Object &&
                rollup.TryGetProperty("reviewCount", out var countEl) &&
                countEl.ValueKind == JsonValueKind.Number)
            {
                return countEl.GetInt32();
            }
            return 0;
        }

        private static List<string> GetImageUrls(JsonElement rootProduct)
        {
            if (rootProduct.TryGetProperty("gallery", out var gallery) &&
                gallery.ValueKind == JsonValueKind.Array)
            {
                var urls = new List<string>();
                foreach (var img in gallery.EnumerateArray())
                    if (img.TryGetProperty("src", out var srcEl) && srcEl.ValueKind == JsonValueKind.String)
                    {
                        var u = srcEl.GetString();
                        if (!string.IsNullOrWhiteSpace(u))
                            urls.Add(u);
                    }
                return urls;
            }
            return new List<string>();
        }

        private static Dictionary<string, Dictionary<string, string>> BuildAttributeValueMap(JsonElement rootProduct)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (rootProduct.TryGetProperty("attributes", out var attributesEl) && attributesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var attr in attributesEl.EnumerateArray())
                {
                    if (!TryGetString(attr, "label", out var attrName) || string.IsNullOrWhiteSpace(attrName))
                        continue;

                    var valueMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (attr.TryGetProperty("options", out var optionsEl) && optionsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var option in optionsEl.EnumerateArray())
                        {
                            if (TryGetString(option, "label", out var label) &&
                                TryGetString(option, "value", out var value) &&
                                !string.IsNullOrWhiteSpace(label) &&
                                !string.IsNullOrWhiteSpace(value))
                            {
                                valueMap[value.Trim()] = label.Trim();
                            }
                        }
                    }
                    if (valueMap.Count > 0)
                        result[attrName.Trim()] = valueMap;
                }
            }
            return result;
        }

        private static string BuildVariantUrl(string baseId, Dictionary<string, string> attributes)
        {
            if (string.IsNullOrEmpty(baseId)) return "";
            var parts = attributes
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .Select(kv => $"{kv.Key.ToLower()}={kv.Value}");
            var joined = string.Join("&", parts);
            return string.IsNullOrEmpty(joined) ? baseId : $"{baseId}${joined}";
        }

        private static Dictionary<string, StarRating> BuildStarRatingDistribution(JsonElement rootRating)
        {
            var dict = new Dictionary<string, StarRating>();
            if (rootRating.TryGetProperty("results", out var results) &&
                results.ValueKind == JsonValueKind.Array &&
                results.GetArrayLength() > 0 &&
                results[0].TryGetProperty("rollup", out var rollup) &&
                rollup.ValueKind == JsonValueKind.Object &&
                rollup.TryGetProperty("ratingHistogram", out var histogramEl) &&
                histogramEl.ValueKind == JsonValueKind.Array)
            {
                var length = histogramEl.GetArrayLength();
                var numbers = new int[length];
                var total = 0;
                for (int i = 0; i < length; i++)
                {
                    numbers[i] = histogramEl[i].GetInt32();
                    total += numbers[i];
                }
                for (int i = 0; i < length; i++)
                {
                    var number = numbers[i];
                    var percentage = total > 0
                        ? Math.Round(number / (double)total * 100, 1)
                        : 0;
                    dict[$"{i + 1} Star"] = new StarRating
                    {
                        Number = number,
                        Percentage = percentage
                    };
                }
            }
            return dict;
        }

        private static List<string> ExtractFeatures(JsonElement rootProduct)
        {
            var list = new List<string>();
            if (rootProduct.TryGetProperty("details", out var detailsEl) && detailsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var section in detailsEl.EnumerateArray())
                {
                    if (TryGetString(section, "id", out var idVal) &&
                        string.Equals(idVal, "productFeatures", StringComparison.OrdinalIgnoreCase) &&
                        section.TryGetProperty("data", out var dataEl) &&
                        dataEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in dataEl.EnumerateArray())
                            if (TryGetString(item, "label", out var label) && !string.IsNullOrWhiteSpace(label))
                                list.Add(label.Trim());
                        break;
                    }
                }
            }
            return list;
        }

        private static string ExtractDescription(JsonElement rootProduct)
        {
            if (rootProduct.TryGetProperty("details", out var detailsEl) && detailsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var section in detailsEl.EnumerateArray())
                {
                    if (TryGetString(section, "label", out var label) &&
                        string.Equals(label, "Description", StringComparison.OrdinalIgnoreCase) &&
                        section.TryGetProperty("data", out var dataEl) &&
                        dataEl.ValueKind == JsonValueKind.Array)
                    {
                        var lines = new List<string>();
                        foreach (var item in dataEl.EnumerateArray())
                            if (item.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                            {
                                var t = textEl.GetString()?.Trim();
                                if (!string.IsNullOrEmpty(t))
                                    lines.Add(t);
                            }
                        return string.Join("\n", lines);
                    }
                }
            }
            return "";
        }

        private static string DetermineAvailability(JsonElement rootProduct)
        {
            if (rootProduct.TryGetProperty("variants", out var variantsEl) && variantsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var variant in variantsEl.EnumerateArray())
                    if (TryGetString(variant, "productInventoryState", out var state) &&
                        string.Equals(state, "InStock", StringComparison.OrdinalIgnoreCase))
                        return "InStock";
                return "OutOfStock";
            }
            if (rootProduct.TryGetProperty("notifyMe", out var notifyEl) && notifyEl.ValueKind == JsonValueKind.False)
                return "InStock";
            return "OutOfStock";
        }
    }
}