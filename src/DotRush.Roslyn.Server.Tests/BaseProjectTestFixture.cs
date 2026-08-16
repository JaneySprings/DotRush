using DotRush.Common;
using DotRush.Common.Extensions;
using DotRush.Common.InteropV2;
using DotRush.Roslyn.Server.Services;
using EmmyLua.LanguageServer.Framework.Server;
using NUnit.Framework;
using FSExtensions = DotRush.Common.Extensions.FileSystemExtensions;

namespace DotRush.Roslyn.Server.Tests;

[TestFixture]
public abstract class BaseProjectTestFixture {
    protected string SandboxDirectory { get; init; }
    protected string ProjectName { get; init; }

    protected WorkspaceService Workspace { get; private set; } = null!;
    protected string ProjectDirectory { get; private set; } = null!;
    protected string ProjectFilePath { get; private set; } = null!;

    protected BaseProjectTestFixture(string name) {
        ProjectName = name;
        SandboxDirectory = Path.Combine(AppContext.BaseDirectory, "Sandbox");
    }

    protected abstract string CreateProjectFileContent();
    protected virtual void OnGlobalSetup() { }
    protected virtual void OnGlobalTearDown() { }
    protected virtual WorkspaceService CreateInitializedWorkspace() {
        var workspace = new TestWorkspaceService(new ConfigurationService(null), null);
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
        SafeExtensions.ThrowOnExceptions = true;
        FSExtensions.TryDeleteDirectory(SandboxDirectory);
        Directory.CreateDirectory(SandboxDirectory);

        Workspace = CreateInitializedWorkspace();
        await Workspace.LoadAsync(new[] { CreateProject() }, CancellationToken.None).ConfigureAwait(false);

        OnGlobalSetup();
    }

    [OneTimeTearDown]
    public void GlobalTearDown() {
        FSExtensions.TryDeleteDirectory(SandboxDirectory);
        Workspace.Dispose();
        Workspace = null!;
        OnGlobalTearDown();
    }

    protected TValue OnPlatform<TValue>(TValue unix, TValue win) {
        return RuntimeInfo.IsWindows ? win : unix;
    }

    private string CreateProject() {
        ProjectDirectory = Path.Combine(SandboxDirectory, ProjectName);
        ProjectFilePath = Path.Combine(ProjectDirectory, $"{ProjectName}.csproj");
        if (Directory.Exists(ProjectDirectory))
            throw new InvalidOperationException($"Project directory '{ProjectDirectory}' already exists.");

        Directory.CreateDirectory(ProjectDirectory);
        File.WriteAllText(ProjectFilePath, CreateProjectFileContent());
        return ProjectFilePath;
    }

    class TestWorkspaceService : WorkspaceService {
        public TestWorkspaceService(ConfigurationService configurationService, LanguageServer? serverFacade) : base(configurationService, serverFacade) { }

        public override void OnProjectRestoreCompleted(string documentPath, ProcessResult result) {
            base.OnProjectRestoreCompleted(documentPath, result);
            var message = string.Join(Environment.NewLine, result.ErrorLines.Count == 0 ? result.OutputLines : result.ErrorLines);
            Assert.That(result.Success, Is.True, $"Restore error: {message}");
        }
    }
}