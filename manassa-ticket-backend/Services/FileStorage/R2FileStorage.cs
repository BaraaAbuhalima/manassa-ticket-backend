using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using manassa_ticket_backend.Configuration;
using Microsoft.Extensions.Options;

namespace manassa_ticket_backend.Services.FileStorage;

public class R2FileStorage(IAmazonS3 s3Client, IOptions<R2Options> options) : IFileStorage
{
    public async Task<(string Key, string UploadUrl)> CreatePresignedUploadUrlAsync(string keyPrefix, TimeSpan expiry)
    {
        var key = $"{keyPrefix}/{Guid.NewGuid()}.pdf";

        var uploadUrl = await s3Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = options.Value.BucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiry),
            ContentType = "application/pdf"
        });

        return (key, uploadUrl);
    }

    public async Task<string> CreatePresignedDownloadUrlAsync(string key, TimeSpan expiry)
    {
        return await s3Client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = options.Value.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry)
        });
    }

    public async Task<Stream?> OpenReadAsync(string key)
    {
        try
        {
            var response = await s3Client.GetObjectAsync(options.Value.BucketName, key);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string key)
    {
        try
        {
            await s3Client.DeleteObjectAsync(options.Value.BucketName, key);
            return true;
        }
        catch (AmazonS3Exception)
        {
            return false;
        }
    }
}
