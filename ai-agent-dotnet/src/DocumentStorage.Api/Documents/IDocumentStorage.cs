namespace DocumentStorage.Api.Documents;

/// <summary>Where documents are kept. The S3 adapter implements it; tests replace it.</summary>
public interface IDocumentStorage
{
	Task SaveAsync(Document document, CancellationToken cancellationToken);

	/// <returns>The document, or <c>null</c> when no document has that id.</returns>
	Task<Document?> FindAsync(DocumentId id, CancellationToken cancellationToken);
}
