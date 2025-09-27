using System.Reflection;
using DotRush.Common.Logging;
using DotRush.Common.MSBuild;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.IDE;

namespace DotRush.Debugging.Host.TemplateEngine;

public class TemplateHostAdapter {
    private static readonly string HostVersion;
    private static readonly Dictionary<string, string> Preferences;
    private readonly Bootstrapper templateEngineBootstrapper;
    private readonly CurrentClassLogger currentClassLogger;
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
        currentClassLogger = new CurrentClassLogger(nameof(TemplateHostAdapter));
        templateEngineBootstrapper = new Bootstrapper(host, virtualizeConfiguration: false, loadDefaultComponents: true);
    }

    public async Task<IEnumerable<ITemplateInfo>> GetTemplatesAsync(CancellationToken cancellationToken) {
        try {
            if (!isInitialized)
                await LoadEmbededTemplatePackages(cancellationToken);

            var templates = await templateEngineBootstrapper.GetTemplatesAsync(cancellationToken);
            return templates.Where(IsProjectTemplate);
        } catch (Exception ex) {
            currentClassLogger.Error(ex);
            return Array.Empty<ITemplateInfo>();
        }
    }
    public async Task<Status> CreateTemplateAsync(string identity, string outputPath, Dictionary<string, string?>? parameters, CancellationToken cancellationToken) {
        try {
            var templates = await GetTemplatesAsync(cancellationToken);
            var template = templates.FirstOrDefault(t => t.Identity == identity);
            if (template == null)
                return Status.Fail("Template not found");

            parameters ??= new Dictionary<string, string?>();
            var result = await templateEngineBootstrapper.CreateAsync(template, Path.GetFileName(outputPath), outputPath, parameters, cancellationToken: cancellationToken);
            if (result == null || result.Status != CreationResultStatus.Success)
                return Status.Fail(result?.ErrorMessage ?? "Unknown error");

            currentClassLogger.Debug($"'{identity}' created at '{outputPath}' | '{result.OutputBaseDirectory}");
            return Status.Success();
        } catch (Exception ex) {
            currentClassLogger.Error(ex);
            return Status.Fail(ex.Message);
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
        // templateDirectory = Path.Combine(MSBuildLocator.GetRootLocation(), "template-packs");
        // if (Directory.Exists(templateDirectory)) {
        //     templatePackages = Directory.GetFiles(templateDirectory, "*.nupkg", SearchOption.TopDirectoryOnly);
        //     if (templatePackages.Length > 0)
        //         await templateEngineBootstrapper.InstallTemplatePackagesAsync(templatePackages.Select(t => new InstallRequest(t)), InstallationScope.Global, cancellationToken);
        // }
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