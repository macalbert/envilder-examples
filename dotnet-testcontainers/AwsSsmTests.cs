namespace Envilder.Examples.Tests;

using System.Net.Http.Json;
using System.Text.Json;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using AwesomeAssertions;
using Envilder.Infrastructure.Aws;

public sealed class AwsSsmTests(LocalStackFixture localStack) : IAsyncLifetime
{
	private readonly List<string> _storedPaths = [];

	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		if (_storedPaths.Count > 0)
		{
			await localStack.Ssm.DeleteParametersAsync(new DeleteParametersRequest { Names = _storedPaths });
		}
	}

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
		var mapFile = TestMapFiles.WithUniqueSecrets(
			"envilder.test.aws.json", path => $"{path}/{Guid.NewGuid():N}");
		var expected = Guid.NewGuid().ToString();

		await StoreAsync(mapFile.Mappings["DEMO_SECRET"], expected);

		var sut = new EnvilderClient(new AwsSsmSecretProvider(localStack.Ssm));

		// Act
		var actual = await sut.ResolveSecretsAsync(mapFile, TestContext.Current.CancellationToken);

		// Assert
		actual["DEMO_SECRET"].Should().Be(expected);
	}

	private async Task StoreAsync(string path, string value)
	{
		_storedPaths.Add(path);

		await localStack.Ssm.PutParameterAsync(
			new PutParameterRequest
			{
				Name = path,
				Value = value,
				Type = ParameterType.SecureString,
			},
			TestContext.Current.CancellationToken);
	}
}
