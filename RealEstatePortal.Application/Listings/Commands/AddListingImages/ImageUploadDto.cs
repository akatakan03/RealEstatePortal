namespace RealEstatePortal.Application.Listings.Commands.AddListingImages;

// Plain data crossing into Application — no ASP.NET IFormFile here (keeps the layer clean).
public record ImageUploadDto(byte[] Content, string FileName, string ContentType);