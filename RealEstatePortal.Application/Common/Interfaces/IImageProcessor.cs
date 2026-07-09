using RealEstatePortal.Application.Common.Models;

namespace RealEstatePortal.Application.Common.Interfaces;

public interface IImageProcessor
{
    // Throws if the bytes aren't a valid image.
    Task<ProcessedImage> ProcessAsync(byte[] input, CancellationToken cancellationToken = default);
}