using System.Reflection;
using DotRush.Common.Logging;
using DotRush.Common.MSBuild;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.IDE;

namespace DotRush.Debugging.Host.TemplateEngine;

public class TemplateHostAdapter {
    private static readonly string HostVersion;
    private static readonly Dictionary<string, string> Preferences;
    private readonly Bootstrapper templateEngineBootstrapper;
    private bool isInitialized;

    static TemplateHostAdapter() {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        HostVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        Preferences = new Dictionary<string, string> {
            { "prefs:language", "C#" }
        };
    }
    public TemplateHostAdapter() {
        var host = TemplateHostAdapter.CreateHost("dotnetcli");
        templateEngineBootstrapper = new Bootstrapper(host, virtualizeConfiguration: false, loadDefaultComponents: true);
    }

    public async Task<IEnumerable<ITemplateInfo>> GetTemplatesAsync(CancellationToken cancellationToken) {
        try {
            if (!isInitialized)
                await LoadEmbededTemplatePackages(cancellationToken);

            var templates = await templateEngineBootstrapper.GetTemplatesAsync(cancellationToken);
            return templates.Where(IsProjectTemplate);
        } catch (Exception ex) {
            CurrentSessionLogger.Error(ex);
            return Array.Empty<ITemplateInfo>();
        }
    }

    private async Task LoadEmbededTemplatePackages(CancellationToken cancellationToken) {
        var templateDirectory = MSBuildLocator.GetTemplatePackagesLocation();
        if (string.IsNullOrEmpty(templateDirectory) || !Directory.Exists(templateDirectory))
            return;

        var templatePackages = Directory.GetFiles(templateDirectory, "*.nupkg", SearchOption.TopDirectoryOnly);
        if (templatePackages.Length == 0)
            return;

        await templateEngineBootstrapper.InstallTemplatePackagesAsync(templatePackages.Select(t => new InstallRequest(t)), InstallationScope.Global, cancellationToken);
        isInitialized = true;
    }
    private static bool IsProjectTemplate(ITemplateInfo template) {
        if (!template.TagsCollection.ContainsKey("type"))
            return false;
        // TODO: allow other languages?
        if (template.TagsCollection.ContainsKey("language") && !template.TagsCollection["language"].Equals("C#", StringComparison.OrdinalIgnoreCase))
            return false;

        return template.TagsCollection["type"] == "project" || template.TagsCollection["type"] == "solution";
    }


    private static DefaultTemplateEngineHost CreateHost(string hostIdentifier) {
        ArgumentException.ThrowIfNullOrEmpty(hostIdentifier);
        return new DefaultTemplateEngineHost(hostIdentifier, HostVersion, Preferences);
    }
}