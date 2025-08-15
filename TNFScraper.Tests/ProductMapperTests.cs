using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using TNFScraper;

namespace TNFScraper.Tests
{
    public class ProductMapperTests
    {
        // Rating JSON
        private const string RatingFull = """
        {
          "results":[
            {
              "rollup":{
                "averageRating":4.5,
                "reviewCount":12,
                "ratingHistogram":[1,2,3,4,2]
              }
            }
          ]
        }
        """;

        private const string RatingNoHistogram = """
        {
          "results":[
            {
              "rollup":{
                "averageRating":3.0,
                "reviewCount":5
              }
            }
          ]
        }
        """;

        private const string RatingEmptyObject = "{}";

        // Product JSON
        private const string ProductFull = """
        {
          "id":"id123",
          "name":"Jacket X",
          "offers":{
            "seller":{"name":"The North Face"},
            "price":199.99
          },
          "currency":"USD",
          "gallery":[
            {"src":"https://example.com/img/1.jpg"},
            {"src":"https://example.com/img/2.jpg"}
          ],
          "url":"/en-us/p/id123.html",
          "productJsonLd":{"mpn":"MPN-123"},
          "attributes":[
            {
              "label":"Color",
              "options":[
                {"label":"Red","value":"RED"},
                {"label":"Blue","value":"BLU"}
              ]
            },
            {
              "label":"Size",
              "options":[
                {"label":"Small","value":"S"},
                {"label":"Large","value":"L"}
              ]
            }
          ],
          "variants":[
            {
              "productInventoryState":"InStock",
              "attributes":{"Color":"RED","Size":"S"}
            },
            {
              "productInventoryState":"OutOfStock",
              "attributes":{"Color":"BLU","Size":"L"}
            }
          ],
          "breadcrumbs":[
            {"label":"Men"},
            {"label":"Jackets"},
            {"label":"Insulated"}
          ],
          "details":[
            {
              "id":"productFeatures",
              "data":[
                {"label":"Waterproof"},
                {"label":"Breathable"}
              ]
            },
            {
              "label":"Description",
              "data":[
                {"text":"Line 1"},
                {"text":"Line 2"}
              ]
            }
          ],
          "badge":{"label":"New"}
        }
        """;

        private const string ProductNoVariantsNotifyFalse = """
        {
          "id":"solo1",
          "name":"Beanie",
          "offers":{"seller":{"name":"The North Face"},"price":29.50},
          "currency":"USD",
          "url":"/p/solo1",
          "productJsonLd":{"mpn":"MPN-BEANIE"},
          "notifyMe": false
        }
        """;

        private const string ProductNoVariantsNotifyTrue = """
        {
          "id":"solo2",
          "name":"Gloves",
          "offers":{"seller":{"name":"The North Face"},"price":49.0},
          "currency":"USD",
          "url":"/p/solo2",
          "productJsonLd":{"mpn":"MPN-GLOVES"},
          "notifyMe": true
        }
        """;

        private const string ProductMinimal = """
        {
          "id":"basic1",
          "offers":{"seller":{"name":"The North Face"}},
          "productJsonLd":{"mpn":""}
        }
        """;

        [Fact]
        public void Map_FullProduct_RootBasicAndUrl()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingFull);

            mapped.RootDomain.Should().Be("thenorthface.com");
            mapped.Name.Should().Be("Jacket X");
            mapped.Brand.Should().Be("The North Face");
            mapped.Currency.Should().Be("USD");
            mapped.Price.Should().Be(199.99);
            mapped.Url.Should().Be("https://www.thenorthface.com/en-us/p/-id123");
        }

        [Fact]
        public void Map_FullProduct_VariantSku_And_VariantMpn()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingFull);
            mapped.Sku.Should().Be("id123$color=RED&size=S");
            mapped.Mpn.Should().Be("MPN-123RED");
        }

        [Fact]
        public void MapAll_ReturnsObjectPerVariant()
        {
            var list = ProductMapper.MapAll(ProductFull, RatingFull);
            list.Should().HaveCount(2);

            var first = list[0];
            var second = list[1];

            first.Sku.Should().Be("id123$color=RED&size=S");
            first.Availability.Should().Be("InStock");
            first.VariantAttributes["Color"].Should().Be("Red");
            first.VariantAttributes["Size"].Should().Be("Small");

            second.Sku.Should().Be("id123$color=BLU&size=L");
            second.Availability.Should().Be("OutOfStock");
            second.VariantAttributes["Color"].Should().Be("Blue");
            second.VariantAttributes["Size"].Should().Be("Large");

            first.Variants.Should().BeEquivalentTo(new[]
            {
                "id123$color=RED&size=S",
                "id123$color=BLU&size=L"
            });
        }

        [Fact]
        public void Map_Variants_VariantsAttributesStructure()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingFull);

            mapped.Variants.Should().BeEquivalentTo(new[]
            {
                "id123$color=RED&size=S",
                "id123$color=BLU&size=L"
            });

            var flattened = mapped.VariantsAttributes
                .SelectMany(d => d)
                .ToDictionary(k => k.Key, v => v.Value);

            flattened.Should().ContainKey("id123$color=RED&size=S");
            flattened["id123$color=RED&size=S"]["Color"].Should().Be("Red");
            flattened["id123$color=RED&size=S"]["Size"].Should().Be("Small");
        }

        [Fact]
        public void Map_NoVariants_FallbackSku()
        {
            var mapped = ProductMapper.Map(ProductNoVariantsNotifyFalse, RatingEmptyObject);
            mapped.Sku.Should().Be("solo1");
            mapped.Variants.Should().BeEmpty();
            mapped.VariantAttributes.Should().BeEmpty();
            mapped.VariantsAttributes.Should().BeEmpty();
        }

        [Fact]
        public void Map_Attributes_ConcatenatedAndBadge()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingFull);

            mapped.Attributes.Should().ContainKey("Color");
            mapped.Attributes["Color"].Should().Be("Red, Blue");
            mapped.Attributes.Should().ContainKey("Size");
            mapped.Attributes["Size"].Should().Be("Small, Large");
            mapped.Attributes.Should().ContainKey("badge");
            mapped.Attributes["badge"].Should().Be("New");
        }

        [Fact]
        public void Map_Categories_WithHomePrefix()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingFull);

            mapped.Category.Should().Be("Home>Men>Jackets>Insulated");
            mapped.CategoryLvl1.Should().Be("Home");
            mapped.CategoryLvl2.Should().Be("Men");
            mapped.CategoryLvl3.Should().Be("Jackets");
            mapped.CategoryLvl4.Should().Be("Insulated");
        }

        [Fact]
        public void Map_Features_AsJsonString()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingFull);
            var features = JsonSerializer.Deserialize<List<string>>(mapped.Features);
            features.Should().BeEquivalentTo(new[] { "Waterproof", "Breathable" });
        }

        [Fact]
        public void Map_Description_LinesJoined()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingFull);
            mapped.Description.Should().Be("Line 1\nLine 2");
        }

        [Fact]
        public void Map_Ratings_FullHistogram()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingFull);

            mapped.AverageCustomerReview.Should().Be(4.5);
            mapped.NumberOfCustomerReviews.Should().Be(12);

            mapped.StarRatingDistribution.Should().HaveCount(5);
            mapped.StarRatingDistribution["1 Star"].Percentage.Should().Be(8.3);
            mapped.StarRatingDistribution["2 Star"].Percentage.Should().Be(16.7);
            mapped.StarRatingDistribution["3 Star"].Percentage.Should().Be(25.0);
            mapped.StarRatingDistribution["4 Star"].Percentage.Should().Be(33.3);
            mapped.StarRatingDistribution["5 Star"].Percentage.Should().Be(16.7);
        }

        [Fact]
        public void Map_Ratings_NoHistogram_EmptyDistribution()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingNoHistogram);
            mapped.AverageCustomerReview.Should().Be(3.0);
            mapped.NumberOfCustomerReviews.Should().Be(5);
            mapped.StarRatingDistribution.Should().BeEmpty();
        }

        [Fact]
        public void Map_Ratings_Missing_DefaultsZero()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingEmptyObject);
            mapped.AverageCustomerReview.Should().Be(0);
            mapped.NumberOfCustomerReviews.Should().Be(0);
            mapped.StarRatingDistribution.Should().BeEmpty();
        }

        [Fact]
        public void Map_Availability_VariantsFirstVariantState()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingFull);
            mapped.Availability.Should().Be("InStock");
        }

        [Fact]
        public void Map_Availability_AllVariantsOutOfStock()
        {
            var allOut = ProductFull.Replace("\"InStock\"", "\"OutOfStock\"");
            var mapped = ProductMapper.Map(allOut, RatingFull);
            mapped.Availability.Should().Be("OutOfStock");
        }

        [Fact]
        public void Map_Availability_NoVariantsNotifyFalse()
        {
            var mapped = ProductMapper.Map(ProductNoVariantsNotifyFalse, RatingEmptyObject);
            mapped.Availability.Should().Be("InStock");
        }

        [Fact]
        public void Map_Availability_NoVariantsNotifyTrue()
        {
            var mapped = ProductMapper.Map(ProductNoVariantsNotifyTrue, RatingEmptyObject);
            mapped.Availability.Should().Be("OutOfStock");
        }

        [Fact]
        public void Map_Availability_NoVariantsNoNotifyProperty()
        {
            var mapped = ProductMapper.Map(ProductMinimal, RatingEmptyObject);
            mapped.Availability.Should().Be("OutOfStock");
        }

        [Fact]
        public void Map_ImageUrls_Unique()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingFull);
            mapped.ImageUrls.Should().HaveCount(2);
            mapped.ImageUrls.Should().OnlyHaveUniqueItems();
            mapped.ImageUrls.Should().Contain(new[]
            {
                "https://example.com/img/1.jpg",
                "https://example.com/img/2.jpg"
            });
        }

        [Fact]
        public void Map_MinimalProduct_Defaults()
        {
            var mapped = ProductMapper.Map(ProductMinimal, RatingEmptyObject);

            mapped.ImageUrls.Should().BeEmpty();
            mapped.Attributes.Should().BeEmpty();
            mapped.Features.Should().Be("[]");
            mapped.Variants.Should().BeEmpty();
            mapped.VariantAttributes.Should().BeEmpty();
            mapped.VariantsAttributes.Should().BeEmpty();
            mapped.Category.Should().Be("Home");
            mapped.Description.Should().BeEmpty();
        }

        [Fact]
        public void Map_DateTime_Format()
        {
            var mapped = ProductMapper.Map(ProductFull, RatingFull);

            DateTime.TryParseExact(mapped.Date, "yyyy-MM-dd'T'HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _).Should().BeTrue();

            DateTime.TryParseExact(mapped.Time, "HH:mm:ss.ffffff",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _).Should().BeTrue();
        }
    }
}