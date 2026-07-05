namespace Envilder.Examples.Tests;

using System.Net.Http.Json;
using System.Text.Json;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AwesomeAssertions;

public sealed class LocalStackTests(LocalStackFixture fixture)
	: IClassFixture<LocalStackFixture>
{
	[Fact]
	public async Task Should_ReportSsmService_When_LocalStackIsRunning()
	{
		// Act
		var health = await fixture.Http.GetFromJsonAsync<JsonElement>(
			"/_localstack/health", TestContext.Current.CancellationToken);

		// Assert
		health.GetProperty("services").GetProperty("ssm").GetString()
			.Should().BeOneOf("available", "running");
	}

	[Fact]
	public async Task Should_RoundTripSecureString_When_LocalStackStartsWithResolvedToken()
	{
		// Arrange
		await fixture.Ssm.PutParameterAsync(
			new PutParameterRequest
			{
				Name = "/demo/secret",
				Value = "hunter2",
				Type = ParameterType.SecureString,
			},
			TestContext.Current.CancellationToken);

		// Act
		var actual = await fixture.Ssm.GetParameterAsync(
			new GetParameterRequest
			{
				Name = "/demo/secret",
				WithDecryption = true,
			},
			TestContext.Current.CancellationToken);

		// Assert
		actual.Parameter.Value.Should().Be("hunter2");
	}
}
