using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Web.Controllers;

[Authorize]   // any logged-in user; temporary test only
public class StorageTestController : Controller
{
    private readonly IFileStorageService _storage;

    public StorageTestController(IFileStorageService storage)
    {
        _storage = storage;
    }

    [HttpGet]
    public IActionResult Index() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]   // 10 MB cap
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Please choose a file.");
            return View("Index");
        }

        var key = $"test/{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";

        await using var stream = file.OpenReadStream();
        await _storage.UploadAsync(stream, key, file.ContentType);

        ViewBag.Url = _storage.GetPublicUrl(key);
        return View("Index");
    }
}