namespace Envilder.Examples.Tests;

using System.Text.Json.Nodes;

/// <summary>
/// A copy of a shared map file with one more variable, written next to the
/// test binaries and deleted on dispose.
/// </summary>
internal sealed class TestMapFile : IDisposable
{
	private TestMapFile(string path) => Path = path;

	public string Path { get; }

	public static TestMapFile WithExtraKey(string source, string variable, string secretPath)
	{
		var map = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
		map[variable] = secretPath;

		var path = $"{System.IO.Path.GetFileNameWithoutExtension(source)}.{Guid.NewGuid():N}.json";
		File.WriteAllText(path, map.ToJsonString());

		return new TestMapFile(path);
	}

	public void Dispose() => File.Delete(Path);
}
