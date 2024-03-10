using Microsoft.Extensions.Configuration;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace DotRush.Server;

public class ConfigurationService {
    private ILanguageServerConfiguration configuration;

    private const string ExtensionId = "dotrush";
    private const string RoslynId = "roslyn";

    private const string WorkspacePropertiesId = $"{ExtensionId}:{RoslynId}:workspaceProperties";
    private const string EnableRoslynAnalyzersId = $"{ExtensionId}:{RoslynId}:enableAnalyzers";
    private const string SkipUnrecognizedProjectsId = $"{ExtensionId}:{RoslynId}:skipUnrecognizedProjects";
    private const string LoadMetadataForReferencedProjectsId = $"{ExtensionId}:{RoslynId}:loadMetadataForReferencedProjects";

    public bool UseRoslynAnalyzers => configuration?.GetValue<bool>(EnableRoslynAnalyzersId) ?? false;
    public bool SkipUnrecognizedProjects => configuration?.GetValue<bool>(SkipUnrecognizedProjectsId) ?? true;
    public bool LoadMetadataForReferencedProjects => configuration?.GetValue<bool>(LoadMetadataForReferencedProjectsId) ?? true;
    public Dictionary<string, string> WorkspaceProperties => GetWorkspaceOptions();

    public ConfigurationService(ILanguageServerConfiguration configuration) {
        this.configuration = configuration;
    }

    public async Task InitializeAsync() {
        var retryCount = 0;
        await Task.Run(() => {
            while (!configuration.AsEnumerable().Any() && retryCount < 25) {
                Thread.Sleep(200);
                retryCount++;
            }
        });
    }
    private Dictionary<string, string> GetWorkspaceOptions() {
        var result = new Dictionary<string, string>();
        for (byte i = 0; i < byte.MaxValue; i++) {
            var option = configuration?.GetValue<string>($"{WorkspacePropertiesId}:{i}");
            if (option == null)
                break;
            
            var keyValue = option.Split('=');
            if (keyValue.Length != 2)
                continue;
            result[keyValue[0]] = keyValue[1];
        }

        return result;
    }
}