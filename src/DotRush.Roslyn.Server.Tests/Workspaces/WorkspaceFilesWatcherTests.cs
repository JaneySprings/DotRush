using DotRush.Common;
using DotRush.Roslyn.Workspaces.Extensions;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class WorkspaceFilesWatcherTests : MultitargetProjectFixture {
    private const int FSDelay = 250;
    // File events are batched and every batch re-evaluates the project with MSBuild
    private static readonly TimeSpan SyncTimeout = TimeSpan.FromSeconds(30);

    protected override void OnGlobalSetup() {
        Workspace.StartObserving();
        WaitLinux();
    }

    [SetUp]
    public void Setup() {
        var lostDocuments = Workspace.Solution!.Projects.SelectMany(p => p.Documents).Where(d => d.Name.Contains(nameof(WorkspaceFilesWatcherTests)));
        foreach (var document in lostDocuments) {
            Workspace.DeleteDocument(document.FilePath!);
            if (File.Exists(document.FilePath))
                File.Delete(document.FilePath!);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    public async Task CreateUpdateDeleteFilesTest(int fileCount) {
        for (int i = 0; i < fileCount; i++)
            CreateFile($"{nameof(WorkspaceFilesWatcherTests)}{i}", "public class TestFile1 {}");
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            Assert.That(await WaitForDocumentsAsync(path, 2).ConfigureAwait(false), Has.Length.EqualTo(2));
        }

        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            File.WriteAllText(path, "public class TestFile2 {}");
        }
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            Assert.That(await WaitForTextAsync(path, "TestFile2").ConfigureAwait(false), Is.True, $"Text of '{path}' was not updated");
        }

        for (int i = 0; i < fileCount; i++)
            DeleteFile($"{nameof(WorkspaceFilesWatcherTests)}{i}");
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            Assert.That(await WaitForDocumentsAsync(path, 0).ConfigureAwait(false), Is.Empty);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    public async Task UpdateFilesViaAtomicRenameTest(int fileCount) {
        for (int i = 0; i < fileCount; i++)
            CreateFile($"{nameof(WorkspaceFilesWatcherTests)}{i}", "public class TestFile1 {}");
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            Assert.That(await WaitForDocumentsAsync(path, 2).ConfigureAwait(false), Has.Length.EqualTo(2));
        }

        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            var tempPath = path + ".tmp";
            File.WriteAllText(tempPath, "public class TestFile2 {}");
            File.Move(tempPath, path, true);
        }
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            Assert.That(await WaitForTextAsync(path, "TestFile2").ConfigureAwait(false), Is.True, $"Text of '{path}' was not updated");
            Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(path).ToArray(), Has.Length.EqualTo(2));
        }

        for (int i = 0; i < fileCount; i++)
            DeleteFile($"{nameof(WorkspaceFilesWatcherTests)}{i}");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    public async Task CreateAndDeleteFilesInFolderTest(int fileCount) {
        for (int i = 0; i < fileCount; i++)
            CreateFile($"{nameof(WorkspaceFilesWatcherTests)}{i}", "TestFolder", "public class TestFile1 {}");
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, "TestFolder", $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            Assert.That(await WaitForDocumentsAsync(path, 2).ConfigureAwait(false), Has.Length.EqualTo(2));
        }

        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, "TestFolder", $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            File.WriteAllText(path, "public class TestFile2 {}");
        }
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, "TestFolder", $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            Assert.That(await WaitForTextAsync(path, "TestFile2").ConfigureAwait(false), Is.True, $"Text of '{path}' was not updated");
        }

        Directory.Delete(Path.Combine(ProjectDirectory, "TestFolder"), true);
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, "TestFolder", $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            Assert.That(await WaitForDocumentsAsync(path, 0).ConfigureAwait(false), Is.Empty);
        }
    }

    [Test, Retry(3)]
    public async Task SkipFilesSyncInIntermidiateFoldersTest() {
        var filePaths = new List<string>();
        foreach (var project in Workspace.Solution!.Projects) {
            var path1 = Path.Combine(project.GetIntermediateOutputPath(), $"{nameof(WorkspaceFilesWatcherTests)}.cs");
            var path2 = Path.Combine(project.GetOutputPath(), $"{nameof(WorkspaceFilesWatcherTests)}.cs");
            try {
                File.WriteAllText(path1, "public class TestFile1 {}");
                File.WriteAllText(path2, "public class TestFile2 {}");
            }
            catch {
                Assert.Fail($"Failed to write files.");
            }

            filePaths.Add(path1);
            filePaths.Add(path2);
        }
        await Task.Delay(FSDelay * 4).ConfigureAwait(false);

        Assert.That(filePaths, Has.Count.EqualTo(4));
        filePaths.ForEach(path => Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(path), Is.Empty));
    }

    [TestCase("g.cs")]
    [TestCase("sg.cs")]
    public async Task CompilerGeneratedFilesFollowProjectItemsTest(string ext) {
        // Generated files inside the project directory are compiled by the SDK's default globs, the ones in obj are not
        var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}.{ext}");
        File.WriteAllText(path, "public class TestFile1 {}");
        var intermediatePath = Path.Combine(Workspace.Solution!.Projects.First().GetIntermediateOutputPath(), $"{nameof(WorkspaceFilesWatcherTests)}.{ext}");
        Directory.CreateDirectory(Path.GetDirectoryName(intermediatePath)!);
        File.WriteAllText(intermediatePath, "public class TestFile2 {}");

        Assert.That(await WaitForDocumentsAsync(path, 2).ConfigureAwait(false), Has.Length.EqualTo(2));
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(intermediatePath), Is.Empty);
        Workspace.DeleteDocument(path);
        File.Delete(path);
    }


    private async Task<DocumentId[]> WaitForDocumentsAsync(string path, int expectedCount) {
        var deadline = DateTime.UtcNow + SyncTimeout;
        DocumentId[] documentIds;
        do {
            documentIds = Workspace.Solution!.GetDocumentIdsWithFilePathV2(path).ToArray();
            if (documentIds.Length == expectedCount)
                return documentIds;
            await Task.Delay(100).ConfigureAwait(false);
        } while (DateTime.UtcNow < deadline);

        return documentIds;
    }
    private async Task<bool> WaitForTextAsync(string path, string expectedContent) {
        var deadline = DateTime.UtcNow + SyncTimeout;
        do {
            var documents = Workspace.Solution!.GetDocumentIdsWithFilePathV2(path).Select(id => Workspace.Solution!.GetDocument(id)).OfType<Document>().ToArray();
            var texts = await Task.WhenAll(documents.Select(d => d.GetTextAsync())).ConfigureAwait(false);
            if (texts.Length != 0 && texts.All(t => t.ToString().Contains(expectedContent)))
                return true;
            await Task.Delay(100).ConfigureAwait(false);
        } while (DateTime.UtcNow < deadline);

        return false;
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

        WaitLinux();

        File.WriteAllText(documentPath, content);
        return documentPath;
    }
    private void DeleteFile(string fileName) {
        var documentPath = Path.Combine(ProjectDirectory, $"{fileName}.cs");
        DotRush.Common.Extensions.FileSystemExtensions.TryDeleteFile(documentPath);
    }

    private void WaitLinux() {
        if (RuntimeInfo.IsLinux) {
            // https://github.com/dotnet/runtime/blob/a6eb1100c1965e3e7ec6f14267e2146ac14fd3b4/src/libraries/System.IO.FileSystem.Watcher/src/System/IO/FileSystemWatcher.Linux.cs#L14-L16
            Thread.Sleep(FSDelay);
        }
    }
}
