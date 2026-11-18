namespace DocumentStorage.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Amazon.S3;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>The whole API, wired as in production, storing in LocalStack's S3.</summary>
public sealed class DocumentApiTests(LocalStackFixture localStack) : IAsyncLifetime
{
	private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

	private readonly TestBucket _bucket = new(localStack.S3);

	private WebApplicationFactory<Program> _factory = null!;

	public async ValueTask InitializeAsync()
	{
		await _bucket.CreateAsync(_ct);

		// Only the S3 client is replaced, so requests go to LocalStack with its test credentials.
		_factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
		{
			builder.UseEnvironment("Production");
			builder.UseSetting("DocumentStorage:BucketName", _bucket.Name);
			builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(localStack.S3)));
		});
	}

	public async ValueTask DisposeAsync()
	{
		await _factory.DisposeAsync();
		await _bucket.DisposeAsync();
	}

	[Fact]
	public async Task Should_DownloadTheUploadedBytesAndContentType_When_DocumentWasUploaded()
	{
		// Arrange
		using var client = _factory.CreateClient();
		var expected = RandomNumberGenerator.GetBytes(64 * 1024);
		var file = new ByteArrayContent(expected);
		file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
		using var form = new MultipartFormDataContent { { file, "file", "report.pdf" } };
		var upload = await client.PostAsync("/documents", form, _ct);
		var location = upload.Headers.Location!;

		// Act
		var actual = await client.GetAsync(location, _ct);

		// Assert
		actual.StatusCode.Should().Be(HttpStatusCode.OK);
		actual.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
		(await actual.Content.ReadAsByteArrayAsync(_ct)).Should().Equal(expected);
	}

	[Fact]
	public async Task Should_Return404_When_DocumentWasNeverUploaded()
	{
		// Arrange
		using var client = _factory.CreateClient();

		// Act
		var actual = await client.GetAsync($"/documents/{Guid.NewGuid()}", _ct);

		// Assert
		actual.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}
}
