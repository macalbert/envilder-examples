namespace DocumentStorage.Api.Documents;

public sealed class InvalidDocumentException(string message) : Exception(message);
