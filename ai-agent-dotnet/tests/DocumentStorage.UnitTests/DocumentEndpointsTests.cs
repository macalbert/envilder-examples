namespace DocumentStorage.UnitTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using DocumentStorage.Api.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>The HTTP contract, with storage replaced in memory: no Docker, no cloud.</summary>
public sealed class DocumentEndpointsTests : IAsyncLifetime
{
	private readonly InMemoryDocumentStorage _storage = new();

	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

	private WebApplicationFactory<Program> _factory = null!;

	public ValueTask InitializeAsync()
	{
		_factory = CreateFactory(_storage);
		return ValueTask.CompletedTask;
	}

	public async ValueTask DisposeAsync()
	{
		await _factory.DisposeAsync();
	}

	[Fact]
	public async Task Should_Return201WithLocationOfTheDocument_When_FileIsUploaded()
	{
		// Arrange
		using var sut = _factory.CreateClient();
		using var form = FileForm([1, 2, 3], "application/pdf");

		// Act
		var actual = await sut.PostAsync("/documents", form, _ct);

		// Assert
		actual.StatusCode.Should().Be(HttpStatusCode.Created);
		var body = await actual.Content.ReadFromJsonAsync<UploadedDocument>(_ct);
		actual.Headers.Location.Should().Be(new Uri($"/documents/{body!.Id}", UriKind.Relative));
		_storage.Documents.Should().ContainSingle().Which.Id.ToString().Should().Be(body.Id);
	}

	[Fact]
	public async Task Should_Return400_When_FileIsEmpty()
	{
		// Arrange
		using var sut = _factory.CreateClient();
		using var form = FileForm([], "text/plain");

		// Act
		var actual = await sut.PostAsync("/documents", form, _ct);

		// Assert
		actual.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Should_Return400_When_FormHasNoFile()
	{
		// Arrange
		using var sut = _factory.CreateClient();
		using var form = new MultipartFormDataContent { { new StringContent("x"), "note" } };

		// Act
		var actual = await sut.PostAsync("/documents", form, _ct);

		// Assert
		actual.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Should_ReturnContentAndContentType_When_DocumentExists()
	{
		// Arrange
		var expected = new Document(DocumentId.New(), "image/png", [9, 8, 7]);
		await _storage.SaveAsync(expected, _ct);
		using var sut = _factory.CreateClient();

		// Act
		var actual = await sut.GetAsync($"/documents/{expected.Id}", _ct);

		// Assert
		actual.StatusCode.Should().Be(HttpStatusCode.OK);
		actual.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
		(await actual.Content.ReadAsByteArrayAsync(_ct)).Should().Equal(expected.Content);
	}

	[Fact]
	public async Task Should_Return404_When_DocumentDoesNotExist()
	{
		// Arrange
		using var sut = _factory.CreateClient();

		// Act
		var actual = await sut.GetAsync($"/documents/{DocumentId.New()}", _ct);

		// Assert
		actual.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Should_Return400_When_IdIsInvalid()
	{
		// Arrange
		using var sut = _factory.CreateClient();

		// Act
		var actual = await sut.GetAsync("/documents/not-a-guid", _ct);

		// Assert
		actual.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Should_Return500WithoutDetails_When_StorageFails()
	{
		// Arrange
		await using var factory = CreateFactory(new FailingDocumentStorage());
		using var sut = factory.CreateClient();

		// Act
		var actual = await sut.GetAsync($"/documents/{DocumentId.New()}", _ct);

		// Assert
		actual.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
		(await actual.Content.ReadAsStringAsync(_ct)).Should().NotContain(FailingDocumentStorage.FailureMessage);
	}

	private static WebApplicationFactory<Program> CreateFactory(IDocumentStorage storage) =>
		new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
		{
			builder.UseEnvironment("Production");
			builder.UseSetting("DocumentStorage:BucketName", "unit-tests");
			builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(storage)));
		});

	private static MultipartFormDataContent FileForm(byte[] content, string contentType)
	{
		var file = new ByteArrayContent(content);
		file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
		return new MultipartFormDataContent { { file, "file", "document.bin" } };
	}

	private sealed record UploadedDocument(string Id);
}
