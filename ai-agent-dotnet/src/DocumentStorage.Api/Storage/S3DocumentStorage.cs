namespace DocumentStorage.Api.Storage;

using Amazon.S3;
using DocumentStorage.Api.Documents;
using Microsoft.Extensions.Options;

/// <summary>Keeps each document as one S3 object, with its content type as the object's Content-Type.</summary>
public sealed class S3DocumentStorage(IAmazonS3 s3, IOptions<DocumentStorageOptions> options) : IDocumentStorage
{
	public Task SaveAsync(Document document, CancellationToken cancellationToken) =>
		throw new NotImplementedException();

	public Task<Document?> FindAsync(DocumentId id, CancellationToken cancellationToken) =>
		throw new NotImplementedException();
}
