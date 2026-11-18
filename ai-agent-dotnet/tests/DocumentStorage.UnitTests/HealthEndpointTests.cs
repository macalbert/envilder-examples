namespace DocumentStorage.UnitTests;

using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

public sealed class HealthEndpointTests : IAsyncLifetime
{
	private WebApplicationFactory<Program> _factory = null!;

	public ValueTask InitializeAsync()
	{
		// The app validates its configuration at startup; no AWS call happens until a document is stored.
		_factory = new WebApplicationFactory<Program>()
			.WithWebHostBuilder(builder => builder.UseSetting("DocumentStorage:BucketName", "unit-tests"));
		return ValueTask.CompletedTask;
	}

	public async ValueTask DisposeAsync()
	{
		await _factory.DisposeAsync();
	}

	[Fact]
	public async Task Should_ReturnOk_When_HealthIsRequested()
	{
		// Arrange
		using var sut = _factory.CreateClient();

		// Act
		var actual = await sut.GetAsync("/health", TestContext.Current.CancellationToken);

		// Assert
		actual.StatusCode.Should().Be(HttpStatusCode.OK);
	}
}
