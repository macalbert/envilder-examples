namespace AppHost.Tests;

using System.Net.Http.Json;
using System.Text.Json;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AwesomeAssertions;
using Envilder;
using Envilder.Infrastructure.Aws;

public sealed class AwsSsmTests(AspireAppFixture app)
{
	[Fact]
	public async Task Should_ActivateLicense_When_AppHostStartsLocalStackWithTokenResolvedByEnvilder()
	{
		// Act
		var info = await app.LocalStackHttp.GetFromJsonAsync<JsonElement>(
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

		await app.Ssm.PutParameterAsync(
			new PutParameterRequest
			{
				Name = mapFile.Mappings["DEMO_SECRET"],
				Value = expected,
				Type = ParameterType.SecureString,
				Overwrite = true,
			},
			TestContext.Current.CancellationToken);

		var sut = new EnvilderClient(new AwsSsmSecretProvider(app.Ssm));

		// Act
		var actual = await sut.ResolveSecretsAsync(mapFile, TestContext.Current.CancellationToken);

		// Assert
		actual["DEMO_SECRET"].Should().Be(expected);
	}
}
