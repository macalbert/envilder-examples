namespace DocumentStorage.Api.Documents;

/// <summary>Identifier of a stored document. Generated on upload, never chosen by the client.</summary>
public readonly record struct DocumentId(Guid Value)
{
	public static DocumentId New() => new(Guid.NewGuid());

	/// <summary>Accepts only a non-empty GUID, so a client cannot address arbitrary storage keys.</summary>
	public static bool TryParse(string? value, out DocumentId id)
	{
		id = default;

		if (!Guid.TryParseExact(value, "D", out var guid) || guid == Guid.Empty)
		{
			return false;
		}

		id = new DocumentId(guid);
		return true;
	}

	public override string ToString() => Value.ToString("D");
}
