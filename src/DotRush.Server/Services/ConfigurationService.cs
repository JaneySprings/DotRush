using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DotRush.Server;

public class ConfigurationService {
    private const string ExtensionId = "dotrush";
    private const string RoslynId = "roslyn";

    private const string EnableRoslynAnalyzersId = $"{ExtensionId}:enableRoslynAnalyzers";
    private const string AdditionalWorkspaceArgumentsId = $"{ExtensionId}:additionalWorkspaceArguments";
    private const string SkipUnrecognizedProjectsId = $"{ExtensionId}:{RoslynId}:skipUnrecognizedProjects";
    private const string LoadMetadataForReferencedProjectsId = $"{ExtensionId}:{RoslynId}:loadMetadataForReferencedProjects";

    private ILanguageServerConfiguration? configuration;

    public async Task InitializeAsync(ILanguageServerConfiguration configuration) {
        var retryCount = 0;
        await Task.Run(() => {
            while (!configuration.AsEnumerable().Any() && retryCount < 25) {
                Thread.Sleep(200);
                retryCount++;
            }
        });
        this.configuration = configuration;
    }

    public Dictionary<string, string> AdditionalWorkspaceArguments() => ConfigurationService.ToWorkspaceOptions(configuration?.GetValue<string>(AdditionalWorkspaceArgumentsId));
    public bool EnableRoslynAnalyzers() => configuration?.GetValue<bool>(EnableRoslynAnalyzersId) ?? false;
    public bool SkipUnrecognizedProjects() => configuration?.GetValue<bool>(SkipUnrecognizedProjectsId) ?? true;
    public bool LoadMetadataForReferencedProjects() => configuration?.GetValue<bool>(LoadMetadataForReferencedProjectsId) ?? true;

    private static Dictionary<string, string> ToWorkspaceOptions(string? options) {
        if (string.IsNullOrEmpty(options))
            return new Dictionary<string, string>();

        var result = new Dictionary<string, string>();
        var pairs = options.Split(' ');
        foreach (var pair in pairs) {
            var keyValue = pair.Split('=');
            if (keyValue.Length != 2)
                continue;
            result[keyValue[0]] = keyValue[1];
        }

        return result;
    }
}