using DotRush.Roslyn.Server.Services;
using NUnit.Framework;
using FSExtensions = DotRush.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Server.Tests;

[TestFixture]
public abstract class BaseProjectTestFixture {
    private string sandboxDirectory;
    private string projectName;

    protected WorkspaceService Workspace { get; private set; } = null!;
    protected string ProjectDirectory { get; private set; } = null!;
    protected string ProjectFilePath { get; private set; } = null!;

    protected BaseProjectTestFixture(string name) {
        projectName = name;
        sandboxDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sandbox");
    }

    protected abstract string CreateProjectFileContent();
    protected virtual void OnGlobalSetup() { }
    protected virtual void OnGlobalTearDown() { }
    protected virtual WorkspaceService CreateInitializedWorkspace() {
        var workspace = new WorkspaceService(new ConfigurationService(), null);
        if (!workspace.InitializeWorkspace())
            throw new InvalidOperationException("Failed to initialize workspace.");

        return workspace;
    }
    protected virtual string CreateDocument(string name, string content) {
        var documentPath = Path.Combine(ProjectDirectory, $"{name}.cs");
        FSExtensions.TryDeleteFile(documentPath);
        File.WriteAllText(documentPath, content);
        Workspace.CreateDocument(documentPath);
        Workspace.UpdateDocument(documentPath);
        return documentPath;
    }

    [OneTimeSetUp]
    public async Task GlobalSetup() {
        FSExtensions.TryDeleteDirectory(sandboxDirectory);
        Directory.CreateDirectory(sandboxDirectory);

        Workspace = CreateInitializedWorkspace();
        await Workspace.LoadAsync(new[] { CreateProject() }, CancellationToken.None).ConfigureAwait(false);

        OnGlobalSetup();
    }

    [OneTimeTearDown]
    public void GlobalTearDown() {
        FSExtensions.TryDeleteDirectory(sandboxDirectory);
        Workspace.Dispose();
        Workspace = null!;
        OnGlobalTearDown();
    }

    private string CreateProject() {
        ProjectDirectory = Path.Combine(sandboxDirectory, projectName);
        ProjectFilePath = Path.Combine(ProjectDirectory, $"{projectName}.csproj");
        if (Directory.Exists(ProjectDirectory))
            throw new InvalidOperationException($"Project directory '{ProjectDirectory}' already exists.");

        Directory.CreateDirectory(ProjectDirectory);
        File.WriteAllText(ProjectFilePath, CreateProjectFileContent());
        return ProjectFilePath;
    }
}