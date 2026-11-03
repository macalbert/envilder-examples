namespace Envilder.Examples.Tests;

/// <summary>
/// Sets process environment variables and puts the previous values back on
/// dispose. <c>Env.FromMapFile</c> builds its own cloud client, so this is how
/// a test aims that client at an emulator.
/// </summary>
internal sealed class EnvironmentScope : IDisposable
{
	private readonly (string Name, string? Value)[] _original;

	public EnvironmentScope(IReadOnlyDictionary<string, string?> variables)
	{
		_original = [.. variables.Keys.Select(n => (n, Environment.GetEnvironmentVariable(n)))];

		foreach (var (name, value) in variables)
		{
			Environment.SetEnvironmentVariable(name, value);
		}
	}

	public void Dispose()
	{
		foreach (var (name, value) in _original)
		{
			Environment.SetEnvironmentVariable(name, value);
		}
	}
}
