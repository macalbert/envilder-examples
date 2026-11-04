namespace Envilder.Examples.Tests;

using System.Text.Json.Nodes;

internal static class TestMapFiles
{
	/// <summary>
	/// Reads a shared map file and gives every mapped secret a name no other
	/// test uses, keeping <c>$config</c> as committed.
	/// </summary>
	public static ParsedMapFile WithUniqueSecrets(string path, Func<string, string> unique)
	{
		var map = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

		foreach (var (variable, secret) in map.ToList())
		{
			if (!variable.StartsWith('$'))
			{
				map[variable] = unique(secret!.GetValue<string>());
			}
		}

		return new MapFileParser().Parse(map.ToJsonString());
	}
}
