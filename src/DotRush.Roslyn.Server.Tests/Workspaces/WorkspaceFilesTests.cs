using DotRush.Roslyn.Workspaces.Extensions;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

public class WorkspaceFilesTests : SimpleWorkspaceFixture {
    [Test]
    public async Task CreateDocumentForExistingDocumentUpdatesContentTest() {
        var projectPath = CreateProject("MyProject");
        var documentPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "TestDocument.cs");
        File.WriteAllText(documentPath, "public class TestFile1 {}");
        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);

        var documentIds = Workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath).ToArray();
        Assert.That(documentIds, Is.Not.Empty);

        File.WriteAllText(documentPath, "public class TestFile2 {}");
        Workspace.CreateDocument(documentPath);

        foreach (var documentId in Workspace.Solution!.GetDocumentIdsWithFilePathV2(documentPath)) {
            var document = Workspace.Solution!.GetDocument(documentId);
            var text = await document!.GetTextAsync().ConfigureAwait(false);
            Assert.That(text.ToString(), Does.Contain("TestFile2"));
        }
    }

}
