using System.Reflection;
using DotRush.Common;
using DotRush.Common.Extensions;
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
    private readonly Bootstrapper templateEngineBootstrapper;
    private readonly CurrentClassLogger currentClassLogger;
    private readonly string templatesTempPath;
    private bool isInitialized;

    static TemplateHostAdapter() {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        HostVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }
    public TemplateHostAdapter() {
        var host = TemplateHostAdapter.CreateHost("dotrush_templateengine_host");
        currentClassLogger = new CurrentClassLogger(nameof(TemplateHostAdapter));
        templatesTempPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".templateengine-packages");
        templateEngineBootstrapper = new Bootstrapper(host, virtualizeConfiguration: true, loadDefaultComponents: true);
    }

    public async Task<IEnumerable<ITemplateInfo>> GetTemplatesAsync(CancellationToken cancellationToken) {
        try {
            if (!isInitialized) {
                await LoadUserTemplatePackagesAsync(cancellationToken);
                await LoadEmbededTemplatePackagesAsync(cancellationToken);
                isInitialized = true;
            }
            var templates = await templateEngineBootstrapper.GetTemplatesAsync(cancellationToken);
            return templates.Where(IsProjectTemplate).ToHashSet(TemplateInfoEqualityComparer.Default);
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

    private async Task LoadEmbededTemplatePackagesAsync(CancellationToken cancellationToken) {
        var templateDirectory = MSBuildLocator.GetTemplatePackagesLocation();
        await LoadTemplatePackagesAsync(templateDirectory, cancellationToken);

        templateDirectory = Path.Combine(MSBuildLocator.GetRootLocation(), "template-packs");
        await LoadTemplatePackagesAsync(templateDirectory, cancellationToken);
    }
    private async Task LoadUserTemplatePackagesAsync(CancellationToken cancellationToken) {
        FileSystemExtensions.TryDeleteDirectory(templatesTempPath);
        Directory.CreateDirectory(templatesTempPath);

        var templatesDirectory = Path.Combine(RuntimeInfo.HomeDirectory, ".templateengine", "packages");
        if (!Directory.Exists(templatesDirectory))
            return;

        // Shadow copy .nuget packages because templateEngine can't install it from cache directly
        foreach (var path in Directory.EnumerateFiles(templatesDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)) {
            var shadowCopyPath = Path.Combine(templatesTempPath, Path.GetFileName(path));
            FileSystemExtensions.TryCopyFile(path, shadowCopyPath, true);
        }

        await LoadTemplatePackagesAsync(templatesTempPath, cancellationToken);
        FileSystemExtensions.TryDeleteDirectory(templatesTempPath);
    }
    private async Task LoadTemplatePackagesAsync(string packagesDirectory, CancellationToken cancellationToken) {
        if (string.IsNullOrEmpty(packagesDirectory) || !Directory.Exists(packagesDirectory))
            return;

        var templatePackages = Directory.GetFiles(packagesDirectory, "*.nupkg", SearchOption.TopDirectoryOnly);
        if (templatePackages.Length == 0)
            return;

        var results = await templateEngineBootstrapper.InstallTemplatePackagesAsync(templatePackages.Select(t => new InstallRequest(t, force: true)), InstallationScope.Global, cancellationToken);
        foreach (var result in results)
            currentClassLogger.Debug($"[{result?.InstallRequest?.DisplayName}]: {result?.Error} - {result?.ErrorMessage}");
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
        return new DefaultTemplateEngineHost(hostIdentifier, HostVersion);
    }
}