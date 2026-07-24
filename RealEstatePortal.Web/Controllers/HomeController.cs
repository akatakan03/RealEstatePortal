using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Web.Models;
using System.Diagnostics;

namespace RealEstatePortal.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // The browse page is the site's front door: it carries the hero, the search and the map.
        // There is no separate landing page, and the scaffolded one this used to render was still
        // the ASP.NET template — which is what the brand link in the header points at.
        public IActionResult Index() => RedirectToAction("Index", "Listings");

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
