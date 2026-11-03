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

	[Fact]
	public async Task Should_ReportEveryKeyAsExists_When_ContractHoldsInSsm()
	{
		// Arrange
		await PutDemoSecretAsync();

		using var _ = localStack.UseAsDefaultAwsEndpoint();

		// Act
		var actual = await Env.FromMapFile("envilder.test.aws.json")
			.ValidateContractAsync(TestContext.Current.CancellationToken);

		// Assert
		actual.IsValid.Should().BeTrue();
		actual.Keys.Should().ContainSingle()
			.Which.Status.Should().Be(SecretExistenceStatus.Exists);
	}

	[Fact]
	public async Task Should_ReportKeyAsMissing_When_SecretIsNotInSsm()
	{
		// Arrange
		await PutDemoSecretAsync();

		using var mapFile = TestMapFile.WithExtraKey(
			"envilder.test.aws.json", "NOT_PUSHED_YET", "/envilder-examples/not-pushed-yet");
		using var _ = localStack.UseAsDefaultAwsEndpoint();

		// Act
		var actual = await Env.FromMapFile(mapFile.Path)
			.ValidateContractAsync(TestContext.Current.CancellationToken);

		// Assert
		actual.IsValid.Should().BeFalse();
		actual.Keys.Should().ContainSingle(k => k.Variable == "NOT_PUSHED_YET")
			.Which.Status.Should().Be(SecretExistenceStatus.Missing);
		actual.Keys.Should().ContainSingle(k => k.Variable == "DEMO_SECRET")
			.Which.Status.Should().Be(SecretExistenceStatus.Exists);
	}

	[Fact]
	public async Task Should_FailTheLoad_When_ContractValidationIsOnAndSecretIsNotInSsm()
	{
		// Arrange
		await PutDemoSecretAsync();

		using var mapFile = TestMapFile.WithExtraKey(
			"envilder.test.aws.json", "NOT_PUSHED_YET", "/envilder-examples/not-pushed-yet");
		using var _ = localStack.UseAsDefaultAwsEndpoint();

		// Act
		var act = () => Env.FromMapFile(mapFile.Path)
			.WithContractValidation()
			.ResolveAsync(TestContext.Current.CancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ContractValidationException>())
			.WithMessage("*NOT_PUSHED_YET (/envilder-examples/not-pushed-yet): missing*");
	}

	private async Task PutDemoSecretAsync()
	{
		var mapFile = new MapFileParser().Parse(File.ReadAllText("envilder.test.aws.json"));

		await localStack.Ssm.PutParameterAsync(
			new PutParameterRequest
			{
				Name = mapFile.Mappings["DEMO_SECRET"],
				Value = Guid.NewGuid().ToString(),
				Type = ParameterType.SecureString,
				Overwrite = true,
			},
			TestContext.Current.CancellationToken);
	}
}
