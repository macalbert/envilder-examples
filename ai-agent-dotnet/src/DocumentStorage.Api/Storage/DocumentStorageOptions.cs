namespace DocumentStorage.Api.Storage;

using System.ComponentModel.DataAnnotations;

/// <summary>Plain configuration, section <c>DocumentStorage</c>. It holds no secret.</summary>
public sealed class DocumentStorageOptions
{
	public const string SectionName = "DocumentStorage";

	[Required]
	public string BucketName { get; set; } = string.Empty;
}
