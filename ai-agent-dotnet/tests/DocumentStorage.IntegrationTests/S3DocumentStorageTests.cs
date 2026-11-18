namespace DocumentStorage.IntegrationTests;

using System.Security.Cryptography;
using Amazon.S3;
using AwesomeAssertions;
using DocumentStorage.Api.Documents;
using DocumentStorage.Api.Storage;
using Microsoft.Extensions.Options;

/// <summary>The real S3 adapter against LocalStack's S3, through the real AWS SDK.</summary>
public sealed class S3DocumentStorageTests(LocalStackFixture localStack) : IAsyncLifetime
{
	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

	private readonly TestBucket _bucket = new(localStack.S3);

	public async ValueTask InitializeAsync()
	{
		await _bucket.CreateAsync(_ct);
	}

	public async ValueTask DisposeAsync()
	{
		await _bucket.DisposeAsync();
	}

	[Fact]
	public async Task Should_StoreOneObjectWithContentType_When_DocumentIsSaved()
	{
		// Arrange
		var sut = Storage(_bucket.Name);
		var document = new Document(DocumentId.New(), "application/pdf", RandomNumberGenerator.GetBytes(1024));

		// Act
		await sut.SaveAsync(document, _ct);

		// Assert
		var actual = await localStack.S3.GetObjectMetadataAsync(_bucket.Name, document.Id.ToString(), _ct);
		actual.Headers.ContentType.Should().Be("application/pdf");
		actual.Headers.ContentLength.Should().Be(1024);
	}

	[Fact]
	public async Task Should_ReturnSameBytesAndContentType_When_SavedDocumentIsFound()
	{
		// Arrange
		var expected = new Document(DocumentId.New(), "image/png", RandomNumberGenerator.GetBytes(256 * 1024));
		var sut = Storage(_bucket.Name);
		await sut.SaveAsync(expected, _ct);

		// Act
		var actual = await sut.FindAsync(expected.Id, _ct);

		// Assert
		actual.Should().NotBeNull();
		actual!.Id.Should().Be(expected.Id);
		actual.ContentType.Should().Be(expected.ContentType);
		actual.Content.Should().Equal(expected.Content);
	}

	[Fact]
	public async Task Should_ReturnNull_When_DocumentDoesNotExist()
	{
		// Arrange
		var sut = Storage(_bucket.Name);

		// Act
		var actual = await sut.FindAsync(DocumentId.New(), _ct);

		// Assert
		actual.Should().BeNull();
	}

	[Fact]
	public async Task Should_NotFindDocument_When_ItWasSavedInAnotherBucket()
	{
		// Arrange
		await using var otherBucket = new TestBucket(localStack.S3);
		await otherBucket.CreateAsync(_ct);
		var document = new Document(DocumentId.New(), "text/plain", [1, 2, 3]);
		await Storage(otherBucket.Name).SaveAsync(document, _ct);
		var sut = Storage(_bucket.Name);

		// Act
		var actual = await sut.FindAsync(document.Id, _ct);

		// Assert
		actual.Should().BeNull();
	}

	[Fact]
	public async Task Should_PropagateFailure_When_BucketDoesNotExist()
	{
		// Arrange
		var sut = Storage($"missing-{Guid.NewGuid():N}");

		// Act
		var act = () => sut.SaveAsync(new Document(DocumentId.New(), "text/plain", [1]), _ct);

		// Assert
		await act.Should().ThrowAsync<AmazonS3Exception>();
	}

	private S3DocumentStorage Storage(string bucketName) =>
		new(localStack.S3, Options.Create(new DocumentStorageOptions { BucketName = bucketName }));
}
