using DotRush.Roslyn.Workspaces.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class WorkspaceFilesWatcherTests : MultitargetProjectFixture {
    private const int FSDelay = 250;

    protected override void OnGlobalSetup() {
        Workspace.StartObserving(new[] { ProjectDirectory });
    }

    [SetUp]
    public void Setup() {
        var result = Workspace.Solution!.Projects.SelectMany(p => p.Documents).Where(d => d.Name.Contains(nameof(WorkspaceFilesWatcherTests)));
        Assert.That(result, Is.Empty, "No documents should exist before creating any files.");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    public async Task CreateAndDeleteFilesTest(int fileCount) {
        for (int i = 0; i < fileCount; i++)
            CreateFile($"{nameof(WorkspaceFilesWatcherTests)}{i}", "public class TestFile1 {}");
        await Task.Delay(FSDelay).ConfigureAwait(false);

        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            var result = Workspace.Solution!.GetDocumentIdsWithFilePathV2(path).ToArray();
            Assert.That(result, Has.Length.EqualTo(2));
        }

        for (int i = 0; i < fileCount; i++)
            DeleteFile($"{nameof(WorkspaceFilesWatcherTests)}{i}");
        await Task.Delay(FSDelay).ConfigureAwait(false);

        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            var result = Workspace.Solution!.GetDocumentIdsWithFilePathV2(path).ToArray();
            Assert.That(result, Is.Empty);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    public async Task CreateAndDeleteFilesInFolderTest(int fileCount) {
        for (int i = 0; i < fileCount; i++)
            CreateFile($"{nameof(WorkspaceFilesWatcherTests)}{i}", "TestFolder", "public class TestFile1 {}");
        await Task.Delay(FSDelay).ConfigureAwait(false);

        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, "TestFolder", $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            var result = Workspace.Solution!.GetDocumentIdsWithFilePathV2(path).ToArray();
            Assert.That(result, Has.Length.EqualTo(2));
        }

        Directory.Delete(Path.Combine(ProjectDirectory, "TestFolder"), true);
        await Task.Delay(FSDelay).ConfigureAwait(false);

        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            var result = Workspace.Solution!.GetDocumentIdsWithFilePathV2(path).ToArray();
            Assert.That(result, Is.Empty);
        }
    }

    private string CreateFile(string fileName, string content) {
        var documentPath = Path.Combine(ProjectDirectory, $"{fileName}.cs");
        File.WriteAllText(documentPath, content);
        return documentPath;
    }
    private string CreateFile(string fileName, string directory, string content) {
        var documentPath = Path.Combine(ProjectDirectory, directory, $"{fileName}.cs");
        var directoryPath = Path.GetDirectoryName(documentPath);
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath!);

        File.WriteAllText(documentPath, content);
        return documentPath;
    }
    private void DeleteFile(string fileName) {
        var documentPath = Path.Combine(ProjectDirectory, $"{fileName}.cs");
        DotRush.Common.Extensions.FileSystemExtensions.TryDeleteFile(documentPath);
    }
}