namespace DocumentStorage.UnitTests;

using System.Collections.Concurrent;
using DocumentStorage.Api.Documents;

internal sealed class InMemoryDocumentStorage : IDocumentStorage
{
	private readonly ConcurrentDictionary<DocumentId, Document> _documents = new();

	public IReadOnlyCollection<Document> Documents => [.. _documents.Values];

	public Task SaveAsync(Document document, CancellationToken cancellationToken)
	{
		_documents[document.Id] = document;
		return Task.CompletedTask;
	}

	public Task<Document?> FindAsync(DocumentId id, CancellationToken cancellationToken) =>
		Task.FromResult(_documents.GetValueOrDefault(id));
}

internal sealed class FailingDocumentStorage : IDocumentStorage
{
	public const string FailureMessage = "storage unavailable";

	public Task SaveAsync(Document document, CancellationToken cancellationToken) =>
		throw new InvalidOperationException(FailureMessage);

	public Task<Document?> FindAsync(DocumentId id, CancellationToken cancellationToken) =>
		throw new InvalidOperationException(FailureMessage);
}
