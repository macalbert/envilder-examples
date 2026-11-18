namespace DocumentStorage.Api.Documents;

public sealed record Document(DocumentId Id, string ContentType, byte[] Content);
