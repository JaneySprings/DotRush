using DotRush.Common;
using DotRush.Roslyn.Workspaces.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class WorkspaceFilesWatcherTests : MultitargetProjectFixture {
    private const int FSDelay = 250;

    protected override void OnGlobalSetup() {
        Workspace.StartObserving(new[] { ProjectDirectory });
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
        await Task.Delay(FSDelay).ConfigureAwait(false);
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            var result = Workspace.Solution!.GetDocumentIdsWithFilePathV2(path).ToArray();
            Assert.That(result, Has.Length.EqualTo(2));
        }

        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            File.WriteAllText(path, "public class TestFile2 {}");
        }
        await Task.Delay(FSDelay).ConfigureAwait(false);
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            var result = Workspace.Solution!.GetDocumentIdsWithFilePathV2(path).ToArray();
            foreach (var documentId in result) {
                var document = Workspace.Solution!.GetDocument(documentId);
                var text = await document!.GetTextAsync().ConfigureAwait(false);
                Assert.That(text.ToString(), Does.Contain("TestFile2"));
            }
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

        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, "TestFolder", $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            File.WriteAllText(path, "public class TestFile2 {}");
        }
        await Task.Delay(FSDelay).ConfigureAwait(false);
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, "TestFolder", $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            var result = Workspace.Solution!.GetDocumentIdsWithFilePathV2(path).ToArray();
            foreach (var documentId in result) {
                var document = Workspace.Solution!.GetDocument(documentId);
                var text = await document!.GetTextAsync().ConfigureAwait(false);
                Assert.That(text.ToString(), Does.Contain("TestFile2"));
            }
        }

        Directory.Delete(Path.Combine(ProjectDirectory, "TestFolder"), true);
        await Task.Delay(FSDelay).ConfigureAwait(false);
        for (int i = 0; i < fileCount; i++) {
            var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}{i}.cs");
            var result = Workspace.Solution!.GetDocumentIdsWithFilePathV2(path).ToArray();
            Assert.That(result, Is.Empty);
        }
    }

    [Test]
    public async Task SkipFilesSyncInIntermidiateFoldersTest() {
        var filePaths = new List<string>();
        foreach (var project in Workspace.Solution!.Projects) {
            var path1 = Path.Combine(project.GetIntermediateOutputPath(), $"{nameof(WorkspaceFilesWatcherTests)}.cs");
            var path2 = Path.Combine(project.GetOutputPath(), $"{nameof(WorkspaceFilesWatcherTests)}.cs");
            File.WriteAllText(path1, "public class TestFile1 {}");
            File.WriteAllText(path2, "public class TestFile2 {}");
            filePaths.Add(path1);
            filePaths.Add(path2);
        }
        await Task.Delay(FSDelay).ConfigureAwait(false);

        Assert.That(filePaths, Has.Count.EqualTo(4));
        filePaths.ForEach(path => Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(path), Is.Empty));
    }

    [TestCase("g.cs")]
    [TestCase("sg.cs")]
    public async Task SkipCompilerGeneratedFilesSyncTest(string ext) {
        var path = Path.Combine(ProjectDirectory, $"{nameof(WorkspaceFilesWatcherTests)}.{ext}");
        File.WriteAllText(path, "public class TestFile1 {}");
        await Task.Delay(FSDelay).ConfigureAwait(false);

        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(path), Is.Empty);
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