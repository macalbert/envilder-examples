namespace DocumentStorage.IntegrationTests;

using Amazon.S3;
using Amazon.S3.Model;

/// <summary>A bucket with a unique name, created for one test and deleted with everything in it afterwards.</summary>
internal sealed class TestBucket(IAmazonS3 s3) : IAsyncDisposable
{
	public string Name { get; } = $"documents-{Guid.NewGuid():N}";

	public async Task CreateAsync(CancellationToken cancellationToken)
	{
		await s3.PutBucketAsync(new PutBucketRequest { BucketName = Name }, cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		var objects = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Name });

		foreach (var s3Object in objects.S3Objects ?? [])
		{
			await s3.DeleteObjectAsync(Name, s3Object.Key);
		}

		await s3.DeleteBucketAsync(Name);
	}
}
