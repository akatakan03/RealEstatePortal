using RealEstatePortal.Application.SavedSearches;
using RealEstatePortal.Domain.Entities;
using RealEstatePortal.Domain.Enums;
using RealEstatePortal.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace RealEstatePortal.Application.UnitTests.SavedSearches;

public class SavedSearchMatcherTests
{
    // A listing that a fully-unconstrained search should match.
    private static Listing Listing(
        ListingType type = ListingType.Sale,
        PropertyType property = PropertyType.Apartment,
        decimal price = 5_000_000m,
        int bedrooms = 3,
        string title = "Sunny flat in Kadıköy",
        string address = "Moda, Kadıköy, İstanbul")
        => new()
        {
            Title = title,
            Address = address,
            ListingType = type,
            PropertyType = property,
            Bedrooms = bedrooms,
            Price = new Money(price, "TRY")
        };

    [Fact]
    public void EmptySearch_MatchesAnyListing()
    {
        var search = new SavedSearch();   // no criteria set
        SavedSearchMatcher.Matches(search, Listing()).ShouldBeTrue();
    }

    [Fact]
    public void ListingType_Mismatch_Fails()
    {
        var search = new SavedSearch { ListingType = ListingType.Rent };
        SavedSearchMatcher.Matches(search, Listing(type: ListingType.Sale)).ShouldBeFalse();
    }

    [Fact]
    public void ListingType_Match_Passes()
    {
        var search = new SavedSearch { ListingType = ListingType.Sale };
        SavedSearchMatcher.Matches(search, Listing(type: ListingType.Sale)).ShouldBeTrue();
    }

    [Fact]
    public void PropertyType_Mismatch_Fails()
    {
        var search = new SavedSearch { PropertyType = PropertyType.House };
        SavedSearchMatcher.Matches(search, Listing(property: PropertyType.Apartment)).ShouldBeFalse();
    }

    [Fact]
    public void MaxPrice_AbovePrice_Passes()
    {
        var search = new SavedSearch { MaxPrice = 6_000_000m };
        SavedSearchMatcher.Matches(search, Listing(price: 5_000_000m)).ShouldBeTrue();
    }

    [Fact]
    public void MaxPrice_BelowPrice_Fails()
    {
        var search = new SavedSearch { MaxPrice = 4_000_000m };
        SavedSearchMatcher.Matches(search, Listing(price: 5_000_000m)).ShouldBeFalse();
    }

    [Fact]
    public void MaxPrice_ExactlyPrice_Passes()   // boundary: ≤, not 
    {
        var search = new SavedSearch { MaxPrice = 5_000_000m };
        SavedSearchMatcher.Matches(search, Listing(price: 5_000_000m)).ShouldBeTrue();
    }

    [Fact]
    public void MinBedrooms_Met_Passes()
    {
        var search = new SavedSearch { MinBedrooms = 2 };
        SavedSearchMatcher.Matches(search, Listing(bedrooms: 3)).ShouldBeTrue();
    }

    [Fact]
    public void MinBedrooms_NotMet_Fails()
    {
        var search = new SavedSearch { MinBedrooms = 4 };
        SavedSearchMatcher.Matches(search, Listing(bedrooms: 3)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("kadıköy")]   // case-insensitive, matches address
    [InlineData("Sunny")]     // matches title
    public void Keyword_FoundInTitleOrAddress_Passes(string keyword)
    {
        var search = new SavedSearch { Keyword = keyword };
        SavedSearchMatcher.Matches(search, Listing()).ShouldBeTrue();
    }

    [Fact]
    public void Keyword_NotFound_Fails()
    {
        var search = new SavedSearch { Keyword = "Beşiktaş" };
        SavedSearchMatcher.Matches(search, Listing(title: "Flat", address: "Kadıköy")).ShouldBeFalse();
    }

    [Fact]
    public void AllCriteria_Met_Passes()
    {
        var search = new SavedSearch
        {
            ListingType = ListingType.Sale,
            PropertyType = PropertyType.Apartment,
            MaxPrice = 6_000_000m,
            MinBedrooms = 2,
            Keyword = "Kadıköy"
        };
        SavedSearchMatcher.Matches(search, Listing()).ShouldBeTrue();
    }

    [Fact]
    public void OneCriterionFails_WholeMatchFails()
    {
        // Everything matches except price — the whole thing must fail.
        var search = new SavedSearch
        {
            ListingType = ListingType.Sale,
            MaxPrice = 1_000_000m   // listing is 5,000,000
        };
        SavedSearchMatcher.Matches(search, Listing()).ShouldBeFalse();
    }
}