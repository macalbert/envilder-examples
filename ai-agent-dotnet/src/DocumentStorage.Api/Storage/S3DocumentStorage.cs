namespace DocumentStorage.Api.Storage;

using Amazon.S3;
using Amazon.S3.Model;
using DocumentStorage.Api.Documents;
using Microsoft.Extensions.Options;

/// <summary>Keeps each document as one S3 object, with its content type as the object's Content-Type.</summary>
public sealed class S3DocumentStorage(IAmazonS3 s3, IOptions<DocumentStorageOptions> options) : IDocumentStorage
{
	private readonly string _bucketName = options.Value.BucketName;

	public async Task SaveAsync(Document document, CancellationToken cancellationToken)
	{
		using var content = new MemoryStream(document.Content, writable: false);

		await s3.PutObjectAsync(
			new PutObjectRequest
			{
				BucketName = _bucketName,
				Key = document.Id.ToString(),
				InputStream = content,
				ContentType = document.ContentType,
			},
			cancellationToken);
	}

	public async Task<Document?> FindAsync(DocumentId id, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await s3.GetObjectAsync(_bucketName, id.ToString(), cancellationToken);
			using var content = new MemoryStream();
			await response.ResponseStream.CopyToAsync(content, cancellationToken);

			return new Document(id, response.Headers.ContentType, content.ToArray());
		}
		catch (AmazonS3Exception exception) when (exception.ErrorCode == "NoSuchKey")
		{
			// Only a missing object means "not found". A missing bucket or a denied
			// request is a configuration problem and must surface as a failure.
			return null;
		}
	}
}
