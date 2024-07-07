using DotRush.Roslyn.Tests.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using Xunit;
using Xunit.Sdk;

namespace DotRush.Roslyn.Tests.WorkspaceTests;

public class DotRushWorkspaceTests : TestFixtureBase, IDisposable {

    [Fact]
    public async Task LoadSimpleProjectTest() {
        var projectPath = TestProjectExtensions.CreateClassLib("MyClassLib");
        var workspace = new TestWorkspace([projectPath]);

        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(workspace.Solution);
        Assert.Single(workspace.Solution.ProjectIds);
        Assert.Equal("MyClassLib", workspace.Solution.Projects.First().Name);
        Assert.Equal(projectPath, workspace.Solution.Projects.First().FilePath);
    }
    [Fact]
    public async Task LoadProjectWithMultiTargetFrameworksTest() {
        var projectPath = TestProjectExtensions.CreateClassLib("MyClassLib", TestProjectExtensions.MultiTargetFramework);
        var workspace = new TestWorkspace([projectPath]);

        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(workspace.Solution);
        Assert.Equal(2, workspace.Solution.ProjectIds.Count);
        Assert.Equal(projectPath, workspace.Solution.Projects.ToArray()[0].FilePath);
        Assert.Equal(projectPath, workspace.Solution.Projects.ToArray()[1].FilePath);

        var projectNames = workspace.Solution.Projects.Select(p => p.Name);
        var targetNames = TestProjectExtensions.MultiTargetFramework.Split(';');
        foreach (var targetName in targetNames)
            Assert.Contains($"MyClassLib({targetName})", projectNames);
    }
    [Fact]
    public async Task GlobalPropertiesForProjectWithMultiTargetFrameworksTest() {
        var projectPath = TestProjectExtensions.CreateClassLib("MyClassLib", TestProjectExtensions.MultiTargetFramework);
        var workspace = new TestWorkspace([projectPath], new Dictionary<string, string> {
            { "TargetFramework", "net8.0" },
        });

        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(workspace.Solution);
        Assert.Single(workspace.Solution.ProjectIds);
        Assert.Equal("MyClassLib", workspace.Solution.Projects.First().Name);
    }
    [Fact]
    public async Task ErrorOnRestoreTest() {
        var projectPath = TestProjectExtensions.CreateProject(@"
<Project Sdk=""Microsoft.NET.Sdk"">
    <PropertyGroup>
        <OutputType>Library</OutputType>
        <TargetFramework>Invalid</TargetFramework>
    </PropertyGroup>
</Project>
        ", "MyClassLib");
        var workspace = new TestWorkspace([projectPath]);
        await Assert.ThrowsAsync<FailException>(() => workspace.LoadSolutionAsync(CancellationToken.None)).ConfigureAwait(false);
    }
    [Fact]
    public async Task AutomaticProjectFinderTest() {
        var workspace = new TestWorkspace([]);

        var invisibleDirectory = Path.Combine(TestProjectExtensions.TestProjectsDirectory, ".hidden");
        Directory.CreateDirectory(invisibleDirectory);
        var directoryInfo = new DirectoryInfo(invisibleDirectory);
        directoryInfo.Attributes |= FileAttributes.Hidden;

        var firstProject = TestProjectExtensions.CreateClassLib("MyClassLib");
        var firstProjectDirectory = Path.GetDirectoryName(firstProject)!;

        var secondProject = TestProjectExtensions.CreateConsoleApp("MyConsoleApp");
        var secondProjectDirectory = Path.GetDirectoryName(secondProject)!;

        var thirdProject = TestProjectExtensions.CreateClassLib("MyClassLib2", null, invisibleDirectory);
        TestProjectExtensions.CreateDocument(Path.Combine(firstProjectDirectory, "Folder", "InnerProject.csproj"), "MyClassLib3");
        TestProjectExtensions.CreateDocument(Path.Combine(secondProjectDirectory, ".meteor", "InnerProject2.csproj"), "MyClassLib4");

        workspace.FindTargetsInWorkspace([TestProjectExtensions.TestProjectsDirectory]);
        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(workspace.Solution);
        Assert.Equal(2, workspace.Solution.ProjectIds.Count);

        var projectNames = workspace.Solution.Projects.Select(p => p.Name);
        Assert.Contains("MyClassLib", projectNames);
        Assert.Contains("MyConsoleApp", projectNames);
        Assert.DoesNotContain("MyClassLib2", projectNames);
    }
    [Fact]
    public async Task SolutionDocumentChangesTest() {
        var singleProject = TestProjectExtensions.CreateClassLib("MyClassLib");
        var singleProjectDirectory = Path.GetDirectoryName(singleProject)!;

        var multipleProject = TestProjectExtensions.CreateClassLib("MyClassLib2", TestProjectExtensions.MultiTargetFramework);
        var multipleProjectDirectory = Path.GetDirectoryName(multipleProject)!;

        var workspace = new TestWorkspace([singleProject, multipleProject]);
        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        // Create, SourceCode, SingleTFM
        var singleProjectSourceCodeDocumentPath = TestProjectExtensions.CreateDocument(Path.Combine(singleProjectDirectory, "Class2.cs"), "class Class2 {}");
        workspace.CreateDocument(singleProjectSourceCodeDocumentPath);
        var singleProjectSourceCodeDocumentId = workspace.GetDocumentIdsWithFilePath(singleProjectSourceCodeDocumentPath).Single();
        var singleProjectSourceCodeDocument = workspace.Solution!.GetDocument(singleProjectSourceCodeDocumentId);
        Assert.Equal(singleProjectSourceCodeDocumentPath, singleProjectSourceCodeDocument!.FilePath);
        Assert.Single(workspace.Solution.GetProjectIdsWithDocumentFilePath(singleProjectSourceCodeDocumentPath));
        // Create, SourceCode, MultiTFM
        var multipleProjectSourceCodeDocumentPath = TestProjectExtensions.CreateDocument(Path.Combine(multipleProjectDirectory, "Class2.cs"), "class Class2 {}");
        workspace.CreateDocument(multipleProjectSourceCodeDocumentPath);
        var multipleProjectSourceCodeDocumentIds = workspace.GetDocumentIdsWithFilePath(multipleProjectSourceCodeDocumentPath);
        Assert.Equal(2, multipleProjectSourceCodeDocumentIds.Count());
        foreach (var multipleProjectSourceCodeDocumentId in multipleProjectSourceCodeDocumentIds) {
            var multipleProjectSourceCodeDocument = workspace.Solution.GetDocument(multipleProjectSourceCodeDocumentId);
            Assert.Equal(multipleProjectSourceCodeDocumentPath, multipleProjectSourceCodeDocument!.FilePath);
        }
        Assert.Equal(2, workspace.Solution.GetProjectIdsWithDocumentFilePath(multipleProjectSourceCodeDocumentPath).Count());

        // Create, Text, SingleTFM
        var singleProjectTextDocumentPath = TestProjectExtensions.CreateDocument(Path.Combine(singleProjectDirectory, "Class2.xaml"), "<Window />");
        workspace.CreateDocument(singleProjectTextDocumentPath);
        var singleProjectTextDocumentId = workspace.GetAdditionalDocumentIdsWithFilePath(singleProjectTextDocumentPath).Single();
        var singleProjectTextDocument = workspace.Solution.GetAdditionalDocument(singleProjectTextDocumentId);
        Assert.Equal(singleProjectTextDocumentPath, singleProjectTextDocument!.FilePath);
        // Create, Text, MultiTFM
        var multipleProjectTextDocumentPath = TestProjectExtensions.CreateDocument(Path.Combine(multipleProjectDirectory, "Class2.xaml"), "<Window />");
        workspace.CreateDocument(multipleProjectTextDocumentPath);
        var multipleProjectTextDocumentIds = workspace.GetAdditionalDocumentIdsWithFilePath(multipleProjectTextDocumentPath);
        Assert.Equal(2, multipleProjectTextDocumentIds.Count());
        foreach (var multipleProjectTextDocumentId in multipleProjectTextDocumentIds) {
            var multipleProjectTextDocument = workspace.Solution.GetAdditionalDocument(multipleProjectTextDocumentId);
            Assert.Equal(multipleProjectTextDocumentPath, multipleProjectTextDocument!.FilePath);
        }

        // Update, SourceCode, SingleTFM
        workspace.UpdateDocument(singleProjectSourceCodeDocumentPath, "class Class2 { void Method() {}}");
        singleProjectSourceCodeDocumentId = workspace.GetDocumentIdsWithFilePath(singleProjectSourceCodeDocumentPath).Single();
        singleProjectSourceCodeDocument = workspace.Solution.GetDocument(singleProjectSourceCodeDocumentId);
        var singleProjectSourceCodeDocumentContent = await singleProjectSourceCodeDocument!.GetTextAsync().ConfigureAwait(false);
        Assert.Equal("class Class2 { void Method() {}}", singleProjectSourceCodeDocumentContent.ToString());
        // Update, SourceCode, MultiTFM
        workspace.UpdateDocument(multipleProjectSourceCodeDocumentPath, "class Class2 { void Method() {}}");
        multipleProjectSourceCodeDocumentIds = workspace.GetDocumentIdsWithFilePath(multipleProjectSourceCodeDocumentPath);
        Assert.Equal(2, multipleProjectSourceCodeDocumentIds.Count());
        foreach (var multipleProjectSourceCodeDocumentId in multipleProjectSourceCodeDocumentIds) {
            var multipleProjectSourceCodeDocument = workspace.Solution.GetDocument(multipleProjectSourceCodeDocumentId);
            var multipleProjectSourceCodeDocumentContent = await multipleProjectSourceCodeDocument!.GetTextAsync().ConfigureAwait(false);
            Assert.Equal("class Class2 { void Method() {}}", multipleProjectSourceCodeDocumentContent.ToString());
        }

        // Update, Text, SingleTFM
        workspace.UpdateDocument(singleProjectTextDocumentPath, "<Window x:Name=\"window\" />");
        singleProjectTextDocumentId = workspace.GetAdditionalDocumentIdsWithFilePath(singleProjectTextDocumentPath).Single();
        singleProjectTextDocument = workspace.Solution.GetAdditionalDocument(singleProjectTextDocumentId);
        var singleProjectTextDocumentContent = await singleProjectTextDocument!.GetTextAsync().ConfigureAwait(false);
        Assert.Equal("<Window x:Name=\"window\" />", singleProjectTextDocumentContent.ToString());
        // Update, Text, MultiTFM
        workspace.UpdateDocument(multipleProjectTextDocumentPath, "<Window x:Name=\"window\" />");
        multipleProjectTextDocumentIds = workspace.GetAdditionalDocumentIdsWithFilePath(multipleProjectTextDocumentPath);
        Assert.Equal(2, multipleProjectTextDocumentIds.Count());
        foreach (var multipleProjectTextDocumentId in multipleProjectTextDocumentIds) {
            var multipleProjectTextDocument = workspace.Solution.GetAdditionalDocument(multipleProjectTextDocumentId);
            var multipleProjectTextDocumentContent = await multipleProjectTextDocument!.GetTextAsync().ConfigureAwait(false);
            Assert.Equal("<Window x:Name=\"window\" />", multipleProjectTextDocumentContent.ToString());
        }

        // Delete, SourceCode, SingleTFM
        workspace.DeleteDocument(singleProjectSourceCodeDocumentPath);
        singleProjectSourceCodeDocumentId = workspace.GetDocumentIdsWithFilePath(singleProjectSourceCodeDocumentPath).SingleOrDefault();
        Assert.Null(singleProjectSourceCodeDocumentId);
        // Delete, SourceCode, MultiTFM
        workspace.DeleteDocument(multipleProjectSourceCodeDocumentPath);
        multipleProjectSourceCodeDocumentIds = workspace.GetDocumentIdsWithFilePath(multipleProjectSourceCodeDocumentPath);
        Assert.Empty(multipleProjectSourceCodeDocumentIds);

        // Delete, Text, SingleTFM
        workspace.DeleteDocument(singleProjectTextDocumentPath);
        singleProjectTextDocumentId = workspace.GetAdditionalDocumentIdsWithFilePath(singleProjectTextDocumentPath).SingleOrDefault();
        Assert.Null(singleProjectTextDocumentId);
        // Delete, Text, MultiTFM
        workspace.DeleteDocument(multipleProjectTextDocumentPath);
        multipleProjectTextDocumentIds = workspace.GetAdditionalDocumentIdsWithFilePath(multipleProjectTextDocumentPath);
        Assert.Empty(multipleProjectTextDocumentIds);

        // DeleteFolder, SourceCode, SingleTFM
        var folderFiles = new List<string>();
        for (int i = 1; i < 5; i++) {
            folderFiles.Add(TestProjectExtensions.CreateDocument(Path.Combine(singleProjectDirectory, "TestFolder", $"Class_{i}.cs"), $"class Class_{i} {{}}"));
            workspace.CreateDocument(folderFiles[i - 1]);
        }
        Assert.Equal(folderFiles.Count, workspace.Solution.GetDocumentIdsWithFolderPath(Path.GetDirectoryName(folderFiles.First())!).Count());
        workspace.DeleteFolder(Path.GetDirectoryName(folderFiles.First())!);
        foreach (var folderFile in folderFiles)
            Assert.Empty(workspace.Solution.GetDocumentIdsWithFilePath(folderFile));
        // DeleteFolder, SourceCode, MultiTFM
        folderFiles.Clear();
        for (int i = 1; i < 5; i++) {
            folderFiles.Add(TestProjectExtensions.CreateDocument(Path.Combine(multipleProjectDirectory, "TestFolder", $"Class_{i}.cs"), $"class Class_{i} {{}}"));
            workspace.CreateDocument(folderFiles[i - 1]);
        }
        Assert.Equal(folderFiles.Count * 2, workspace.Solution.GetDocumentIdsWithFolderPath(Path.GetDirectoryName(folderFiles.First())!).Count());
        workspace.DeleteFolder(Path.GetDirectoryName(folderFiles.First())!);
        foreach (var folderFile in folderFiles)
            Assert.Empty(workspace.Solution.GetDocumentIdsWithFilePath(folderFile));

        // DeleteFolder, Text, SingleTFM
        folderFiles.Clear();
        for (int i = 1; i < 5; i++) {
            folderFiles.Add(TestProjectExtensions.CreateDocument(Path.Combine(singleProjectDirectory, "TestFolder", $"File_{i}.xaml"), $"<Window{i} />"));
            workspace.CreateDocument(folderFiles[i - 1]);
        }
        Assert.Equal(folderFiles.Count, workspace.Solution.GetAdditionalDocumentIdsWithFolderPath(Path.GetDirectoryName(folderFiles.First())!).Count());
        workspace.DeleteFolder(Path.GetDirectoryName(folderFiles.First())!);
        foreach (var folderFile in folderFiles)
            Assert.Empty(workspace.GetAdditionalDocumentIdsWithFilePath(folderFile));
        // DeleteFolder, Text, MultiTFM
        folderFiles.Clear();
        for (int i = 1; i < 5; i++) {
            folderFiles.Add(TestProjectExtensions.CreateDocument(Path.Combine(multipleProjectDirectory, "TestFolder", $"File{i}.xaml"), $"File_{i}.xaml"));
            workspace.CreateDocument(folderFiles[i - 1]);
        }
        Assert.Equal(folderFiles.Count * 2, workspace.Solution.GetAdditionalDocumentIdsWithFolderPath(Path.GetDirectoryName(folderFiles.First())!).Count());
        workspace.DeleteFolder(Path.GetDirectoryName(folderFiles.First())!);
        foreach (var folderFile in folderFiles)
            Assert.Empty(workspace.GetAdditionalDocumentIdsWithFilePath(folderFile));
    }

    [Fact]
    public async Task SolutionChangesInIntermidiatePathTest() {
        var projectPath = TestProjectExtensions.CreateClassLib("MyClassLib", TestProjectExtensions.MultiTargetFramework);
        var projectDirectory = Path.GetDirectoryName(projectPath)!;

        var workspace = new TestWorkspace([projectPath]);
        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        var documentPath = TestProjectExtensions.CreateDocument(Path.Combine(projectDirectory, "obj", "Class2.cs"), "class Class2 {}");
        workspace.CreateDocument(documentPath);
        var documentIds = workspace.GetDocumentIdsWithFilePath(documentPath);
        Assert.Equal(2, documentIds.Count());

        workspace.UpdateDocument(documentPath, "class Class2 { void Method() {}}");
        foreach (var documentId in documentIds) {
            var document = workspace.Solution!.GetDocument(documentId);
            Assert.Equal(documentPath, document!.FilePath);
            Assert.Single(document!.Folders);
            Assert.Equal("obj", document.Folders[0]);
            var documentContent = await document.GetTextAsync().ConfigureAwait(false);
            Assert.Equal("class Class2 { void Method() {}}", documentContent.ToString());
        }
    }

    public void Dispose() {
        if (Directory.Exists(TestProjectExtensions.TestProjectsDirectory))
            Directory.Delete(TestProjectExtensions.TestProjectsDirectory, true);
    }
}