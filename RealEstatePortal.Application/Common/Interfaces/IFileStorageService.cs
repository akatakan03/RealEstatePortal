namespace RealEstatePortal.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadAsync(Stream content, string objectKey, string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);

    string GetPublicUrl(string objectKey);
}