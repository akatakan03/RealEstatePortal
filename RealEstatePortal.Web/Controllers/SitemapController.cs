using System.Text;
using System.Xml;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Sitemap.Queries.GetSitemapEntries;
using RealEstatePortal.Web.Localization;

namespace RealEstatePortal.Web.Controllers;

public class SitemapController : Controller
{
    private const string XhtmlNamespace = "http://www.w3.org/1999/xhtml";

    private readonly ISender _sender;

    public SitemapController(ISender sender) => _sender = sender;

    // The file itself stays unlocalized — crawlers expect it at a fixed address — but it lists
    // every page once per language, and each entry declares the others as alternates. Without
    // those hreflang links a search engine treats /tr/… and /en/… as duplicate content and
    // shows only one, which would waste the whole reason for putting the language in the URL.
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
            writer.WriteAttributeString("xmlns", "xhtml", null, XhtmlNamespace);

            WriteLocalized(writer, baseUrl, string.Empty);      // home
            WriteLocalized(writer, baseUrl, "/Listings");       // browse

            // Each active listing at its canonical slug URL.
            foreach (var e in entries)
            {
                WriteLocalized(writer, baseUrl, $"/listing/{e.Id}/{e.Slug}", e.LastModified);
                await writer.FlushAsync();
            }

            writer.WriteEndElement();
            await writer.WriteEndDocumentAsync();
        }

        return Content(sb.ToString(), "application/xml", Encoding.UTF8);
    }

    // One <url> per language for the same page, each carrying the full set of alternates —
    // the self-reference included, which the spec asks for.
    //
    // The path after the language segment is the same in every language: slugs come from the
    // listing's own title, so there is nothing in them to translate.
    private static void WriteLocalized(
        XmlWriter writer, string baseUrl, string path, DateTimeOffset? lastModified = null)
    {
        foreach (var culture in SupportedCultures.Codes)
        {
            writer.WriteStartElement("url");
            writer.WriteElementString("loc", $"{baseUrl}/{culture}{path}");

            foreach (var alternate in SupportedCultures.Codes)
                WriteAlternate(writer, alternate, $"{baseUrl}/{alternate}{path}");

            // Where to send a visitor whose language the site doesn't speak.
            WriteAlternate(writer, "x-default", $"{baseUrl}/{SupportedCultures.Default}{path}");

            if (lastModified is not null)
                writer.WriteElementString("lastmod", lastModified.Value.ToString("yyyy-MM-dd"));

            writer.WriteEndElement();
        }
    }

    private static void WriteAlternate(XmlWriter writer, string hreflang, string href)
    {
        writer.WriteStartElement("link", XhtmlNamespace);
        writer.WriteAttributeString("rel", "alternate");
        writer.WriteAttributeString("hreflang", hreflang);
        writer.WriteAttributeString("href", href);
        writer.WriteEndElement();
    }
}
