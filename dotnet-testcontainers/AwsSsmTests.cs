namespace Envilder.Examples.Tests;

using System.Net.Http.Json;
using System.Text.Json;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AwesomeAssertions;
using Envilder.Infrastructure.Aws;

public sealed class AwsSsmTests(LocalStackFixture localStack)
	: IClassFixture<LocalStackFixture>
{
	[Fact]
	public async Task Should_ActivateLicense_When_LocalStackStartsWithTokenResolvedByEnvilder()
	{
		// Act
		var info = await localStack.Http.GetFromJsonAsync<JsonElement>(
			"/_localstack/info", TestContext.Current.CancellationToken);

		// Assert
		info.GetProperty("is_license_activated").GetBoolean().Should().BeTrue();
	}

	[Fact]
	public async Task Should_ResolveSecretFromSsm_When_MapFilePointsToIt()
	{
		// Arrange
		var mapFile = new MapFileParser().Parse(File.ReadAllText("envilder.test.aws.json"));
		var expected = Guid.NewGuid().ToString();

		await localStack.Ssm.PutParameterAsync(
			new PutParameterRequest
			{
				Name = mapFile.Mappings["DEMO_SECRET"],
				Value = expected,
				Type = ParameterType.SecureString,
				Overwrite = true,
			},
			TestContext.Current.CancellationToken);

		var sut = new EnvilderClient(new AwsSsmSecretProvider(localStack.Ssm));

		// Act
		var actual = await sut.ResolveSecretsAsync(mapFile, TestContext.Current.CancellationToken);

		// Assert
		actual["DEMO_SECRET"].Should().Be(expected);
	}
}
