using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using RealEstatePortal.Application.Common.Interfaces;

namespace RealEstatePortal.Infrastructure.Storage;

public class R2FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly R2Settings _settings;

    public R2FileStorageService(IAmazonS3 s3, IOptions<R2Settings> settings)
    {
        _s3 = s3;
        _settings = settings.Value;
    }

    public async Task<string> UploadAsync(Stream content, string objectKey,
        string contentType, CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,

            // Required for Cloudflare R2 — it doesn't support the SDK's streaming
            // SigV4 / default CRC32 checksums. Without these, uploads fail.
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };

        await _s3.PutObjectAsync(request, cancellationToken);
        return objectKey;
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        await _s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = objectKey
        }, cancellationToken);
    }

    public string GetPublicUrl(string objectKey) =>
        $"{_settings.PublicUrl.TrimEnd('/')}/{objectKey}";
}