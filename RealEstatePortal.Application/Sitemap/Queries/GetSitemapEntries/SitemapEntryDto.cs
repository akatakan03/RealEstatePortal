namespace RealEstatePortal.Application.Sitemap.Queries.GetSitemapEntries;

public class SitemapEntryDto
{
    public int Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public DateTimeOffset LastModified { get; init; }
}