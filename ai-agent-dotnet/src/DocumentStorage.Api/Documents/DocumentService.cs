namespace DocumentStorage.Api.Documents;

/// <summary>The two use cases: upload a document and download it.</summary>
public sealed class DocumentService(IDocumentStorage storage)
{
	public const int MaxContentBytes = 10 * 1024 * 1024;

	public const string DefaultContentType = "application/octet-stream";

	public Task<DocumentId> UploadAsync(byte[] content, string? contentType, CancellationToken cancellationToken) =>
		throw new NotImplementedException();

	public Task<Document?> DownloadAsync(DocumentId id, CancellationToken cancellationToken) =>
		throw new NotImplementedException();
}
