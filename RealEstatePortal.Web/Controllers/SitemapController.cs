using System.Text;
using System.Xml;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Sitemap.Queries.GetSitemapEntries;

namespace RealEstatePortal.Web.Controllers;

public class SitemapController : Controller
{
    private readonly ISender _sender;

    public SitemapController(ISender sender) => _sender = sender;

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Index()
    {
        var entries = await _sender.Send(new GetSitemapEntriesQuery());
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var sb = new StringBuilder();
        var settings = new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8, Async = true };

        await using (var writer = XmlWriter.Create(sb, settings))
        {
            await writer.WriteStartDocumentAsync();
            writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            // Home page
            writer.WriteStartElement("url");
            writer.WriteElementString("loc", $"{baseUrl}/");
            writer.WriteEndElement();

            // Public browse page
            writer.WriteStartElement("url");
            writer.WriteElementString("loc", $"{baseUrl}/Listings");
            writer.WriteEndElement();

            // Each active listing at its canonical slug URL
            foreach (var e in entries)
            {
                writer.WriteStartElement("url");
                writer.WriteElementString("loc", $"{baseUrl}/listing/{e.Id}/{e.Slug}");
                writer.WriteElementString("lastmod", e.LastModified.ToString("yyyy-MM-dd"));
                await writer.FlushAsync();
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            await writer.WriteEndDocumentAsync();
        }

        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }
}