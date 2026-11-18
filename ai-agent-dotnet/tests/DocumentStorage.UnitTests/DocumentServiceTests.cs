namespace DocumentStorage.UnitTests;

using AwesomeAssertions;
using DocumentStorage.Api.Documents;

public sealed class DocumentServiceTests
{
	private readonly InMemoryDocumentStorage _storage = new();

	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

	[Fact]
	public async Task Should_StoreContentAndContentType_When_DocumentIsValid()
	{
		// Arrange
		var sut = new DocumentService(_storage);
		byte[] content = [1, 2, 3];

		// Act
		var actual = await sut.UploadAsync(content, "application/pdf", _ct);

		// Assert
		_storage.Documents.Should().ContainSingle()
			.Which.Should().BeEquivalentTo(new Document(actual, "application/pdf", content));
	}

	[Fact]
	public async Task Should_RejectDocument_When_ContentIsEmpty()
	{
		// Arrange
		var sut = new DocumentService(_storage);

		// Act
		var act = () => sut.UploadAsync([], "text/plain", _ct);

		// Assert
		await act.Should().ThrowAsync<InvalidDocumentException>();
	}

	[Fact]
	public async Task Should_RejectDocument_When_ContentExceedsMaximumSize()
	{
		// Arrange
		var sut = new DocumentService(_storage);
		var content = new byte[DocumentService.MaxContentBytes + 1];

		// Act
		var act = () => sut.UploadAsync(content, "text/plain", _ct);

		// Assert
		await act.Should().ThrowAsync<InvalidDocumentException>();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task Should_StoreAsOctetStream_When_ContentTypeIsMissing(string? contentType)
	{
		// Arrange
		var sut = new DocumentService(_storage);

		// Act
		await sut.UploadAsync([1], contentType, _ct);

		// Assert
		_storage.Documents.Should().ContainSingle()
			.Which.ContentType.Should().Be(DocumentService.DefaultContentType);
	}

	[Fact]
	public async Task Should_ReturnStoredDocument_When_DocumentExists()
	{
		// Arrange
		var expected = new Document(DocumentId.New(), "image/png", [9, 8, 7]);
		await _storage.SaveAsync(expected, _ct);
		var sut = new DocumentService(_storage);

		// Act
		var actual = await sut.DownloadAsync(expected.Id, _ct);

		// Assert
		actual.Should().BeEquivalentTo(expected);
	}

	[Fact]
	public async Task Should_ReturnNull_When_DocumentDoesNotExist()
	{
		// Arrange
		var sut = new DocumentService(_storage);

		// Act
		var actual = await sut.DownloadAsync(DocumentId.New(), _ct);

		// Assert
		actual.Should().BeNull();
	}

	[Fact]
	public async Task Should_PropagateFailure_When_StorageFailsOnUpload()
	{
		// Arrange
		var sut = new DocumentService(new FailingDocumentStorage());

		// Act
		var act = () => sut.UploadAsync([1], "text/plain", _ct);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(FailingDocumentStorage.FailureMessage);
	}

	[Fact]
	public async Task Should_PropagateFailure_When_StorageFailsOnDownload()
	{
		// Arrange
		var sut = new DocumentService(new FailingDocumentStorage());

		// Act
		var act = () => sut.DownloadAsync(DocumentId.New(), _ct);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(FailingDocumentStorage.FailureMessage);
	}
}
