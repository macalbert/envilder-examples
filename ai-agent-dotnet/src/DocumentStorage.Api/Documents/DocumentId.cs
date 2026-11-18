namespace DocumentStorage.Api.Documents;

/// <summary>Identifier of a stored document. Generated on upload, never chosen by the client.</summary>
public readonly record struct DocumentId(Guid Value)
{
	public static DocumentId New() => throw new NotImplementedException();

	public static bool TryParse(string? value, out DocumentId id) => throw new NotImplementedException();

	public override string ToString() => Value.ToString("D");
}
