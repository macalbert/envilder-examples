namespace DocumentStorage.UnitTests;

using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

public sealed class HealthEndpointTests : IAsyncLifetime
{
	private WebApplicationFactory<Program> _factory = null!;

	public ValueTask InitializeAsync()
	{
		_factory = new WebApplicationFactory<Program>();
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
