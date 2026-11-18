namespace DocumentStorage.Api.Documents;

/// <summary>The two use cases: upload a document and download it.</summary>
public sealed class DocumentService(IDocumentStorage storage)
{
	public const int MaxContentBytes = 10 * 1024 * 1024;

	public const string DefaultContentType = "application/octet-stream";

	public async Task<DocumentId> UploadAsync(byte[] content, string? contentType, CancellationToken cancellationToken)
	{
		if (content.Length == 0)
		{
			throw new InvalidDocumentException("The document is empty.");
		}

		if (content.Length > MaxContentBytes)
		{
			throw new InvalidDocumentException($"The document is larger than {MaxContentBytes} bytes.");
		}

		var document = new Document(
			DocumentId.New(),
			string.IsNullOrWhiteSpace(contentType) ? DefaultContentType : contentType,
			content);

		await storage.SaveAsync(document, cancellationToken);

		return document.Id;
	}

	public Task<Document?> DownloadAsync(DocumentId id, CancellationToken cancellationToken) =>
		storage.FindAsync(id, cancellationToken);
}
