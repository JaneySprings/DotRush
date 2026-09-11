using System.Text.Json;
using DotRush.Common.InteropV2;
using DotRush.Common.Logging;
using DotRush.Common.MSBuild;

namespace DotRush.Roslyn.Workspaces.MSBuild;

/// <summary>
/// Runs 'dotnet msbuild' with the -getProperty/-getItem switches and parses the JSON it writes.
/// Every call is a separate process, so the SDK selection (global.json, roll-forward) is entirely MSBuild's own.
/// </summary>
internal static class MSBuildCli {
    public static async Task<MSBuildResult> RunAsync(MSBuildRequest request, CancellationToken cancellationToken) {
        var resultFilePath = Path.Combine(Path.GetTempPath(), $"dotrush-msbuild-{Guid.NewGuid():N}.json");
        try {
            var processInfo = ProcessRunner.CreateProcess(MSBuildLocator.GetMuxerPath(), request.ToArguments(resultFilePath), workingDirectory: Path.GetDirectoryName(request.ProjectPath), captureOutput: true, displayWindow: false, cancellationToken: cancellationToken);
            var processResult = await processInfo.Task.ConfigureAwait(false);
            foreach (var line in processResult.OutputLines.Concat(processResult.ErrorLines).Where(IsErrorLine).Distinct())
                CurrentSessionLogger.Error(line);

            var json = File.Exists(resultFilePath) ? await File.ReadAllTextAsync(resultFilePath, cancellationToken).ConfigureAwait(false) : string.Empty;
            if (!string.IsNullOrWhiteSpace(json))
                return MSBuildResult.Parse(json);

            CurrentSessionLogger.Error($"MSBuild produced no result for '{request.ProjectPath}' (exit code {processResult.ExitCode}): {string.Join(Environment.NewLine, processResult.ErrorLines.Concat(processResult.OutputLines).Take(20))}");
            return MSBuildResult.Empty;
        } finally {
            File.Delete(resultFilePath);
        }
    }
    public static async Task<ProcessResult> RestoreAsync(string targetPath, CancellationToken cancellationToken) {
        var processInfo = ProcessRunner.CreateProcess(MSBuildLocator.GetMuxerPath(), $"restore \"{targetPath}\" -nodeReuse:false", captureOutput: true, displayWindow: false, cancellationToken: cancellationToken);
        var result = await processInfo.Task.ConfigureAwait(false);
        if (result.ExitCode != 0) {
            foreach (var line in result.OutputLines.Concat(result.ErrorLines))
                CurrentSessionLogger.Error(line);
        }
        return result;
    }

    private static bool IsErrorLine(string line) {
        return line.Contains(": error ", StringComparison.Ordinal) || line.StartsWith("MSBUILD : error", StringComparison.Ordinal);
    }
}

internal sealed class MSBuildRequest {
    public required string ProjectPath { get; init; }
    /// <summary>Targets to run before reading properties and items, null evaluates only.</summary>
    public string? Targets { get; init; }
    public Dictionary<string, string> GlobalProperties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public List<string> Properties { get; } = new List<string>();
    public List<string> Items { get; } = new List<string>();

    public string ToArguments(string resultFilePath) {
        var arguments = new List<string> { "msbuild", Quote(ProjectPath), "-nologo", "-nodeReuse:false" };
        if (!string.IsNullOrEmpty(Targets))
            arguments.Add(Quote($"-t:{Targets}"));
        foreach (var (name, value) in GlobalProperties)
            arguments.Add(Quote($"-p:{name}={value}"));
        // A single property without items is printed as plain text, keep at least two so the output is always JSON
        var properties = Properties.Count == 1 ? Properties.Append("MSBuildProjectFullPath") : Properties;
        if (properties.Any())
            arguments.Add($"-getProperty:{string.Join(',', properties)}");
        if (Items.Count != 0)
            arguments.Add($"-getItem:{string.Join(',', Items)}");
        arguments.Add(Quote($"-getResultOutputFile:{resultFilePath}"));
        return string.Join(' ', arguments);
    }
    private static string Quote(string value) {
        return $"\"{value.Replace("\"", "\\\"")}\"";
    }
}

internal sealed class MSBuildResult {
    public static readonly MSBuildResult Empty = new MSBuildResult();

    private readonly Dictionary<string, string> properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<MSBuildItem>> items = new Dictionary<string, List<MSBuildItem>>(StringComparer.OrdinalIgnoreCase);

    public bool IsEmpty => properties.Count == 0 && items.Count == 0;

    public string GetProperty(string name) => properties.GetValueOrDefault(name) ?? string.Empty;
    public IEnumerable<MSBuildItem> GetItems(string itemType) => items.GetValueOrDefault(itemType) ?? Enumerable.Empty<MSBuildItem>();

    public static MSBuildResult Parse(string json) {
        var result = new MSBuildResult();
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("Properties", out var propertiesElement)) {
            foreach (var property in propertiesElement.EnumerateObject())
                result.properties[property.Name] = property.Value.GetString() ?? string.Empty;
        }
        if (document.RootElement.TryGetProperty("Items", out var itemsElement)) {
            foreach (var itemType in itemsElement.EnumerateObject())
                result.items[itemType.Name] = itemType.Value.EnumerateArray().Select(MSBuildItem.Parse).ToList();
        }
        return result;
    }
}

internal sealed class MSBuildItem {
    private readonly Dictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string Identity => GetMetadata("Identity");
    public string FullPath => GetMetadata("FullPath");

    public string GetMetadata(string name) => metadata.GetValueOrDefault(name) ?? string.Empty;

    public static MSBuildItem Parse(JsonElement element) {
        var item = new MSBuildItem();
        foreach (var property in element.EnumerateObject())
            item.metadata[property.Name] = property.Value.GetString() ?? string.Empty;
        return item;
    }
}
