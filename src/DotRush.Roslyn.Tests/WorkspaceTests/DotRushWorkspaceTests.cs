using DotRush.Roslyn.Common.Extensions;
using DotRush.Roslyn.Workspaces.Extensions;
using Xunit;
using Xunit.Sdk;

namespace DotRush.Roslyn.Tests.WorkspaceTests;

public class DotRushWorkspaceTests : MSBuildTestFixture, IDisposable {

    public DotRushWorkspaceTests() {
        SafeExtensions.ThrowOnExceptions = true;
    }

    [Fact]
    public async Task LoadSimpleProjectTest() {
        var projectPath = CreateClassLib("MyClassLib");
        var workspace = new TestWorkspace([projectPath]);

        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(workspace.Solution);
        Assert.Single(workspace.Solution.ProjectIds);
        Assert.Equal("MyClassLib", workspace.Solution.Projects.First().Name);
        Assert.Equal(projectPath, workspace.Solution.Projects.First().FilePath);
    }
    [Fact]
    public async Task LoadProjectWithMultiTargetFrameworksTest() {
        var projectPath = CreateClassLib("MyClassLib", MultiTargetFramework);
        var workspace = new TestWorkspace([projectPath]);

        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(workspace.Solution);
        Assert.Equal(2, workspace.Solution.ProjectIds.Count);
        Assert.Equal(projectPath, workspace.Solution.Projects.ToArray()[0].FilePath);
        Assert.Equal(projectPath, workspace.Solution.Projects.ToArray()[1].FilePath);

        var projectNames = workspace.Solution.Projects.Select(p => p.Name);
        var targetNames = MultiTargetFramework.Split(';');
        foreach (var targetName in targetNames)
            Assert.Contains($"MyClassLib({targetName})", projectNames);
    }
    [Fact]
    public async Task GlobalPropertiesForProjectWithMultiTargetFrameworksTest() {
        var projectPath = CreateClassLib("MyClassLib", MultiTargetFramework);
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
        
        var projectPath = CreateProject(@"
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
        var invisibleDirectory = Path.Combine(MockProjectsDirectory, ".hidden");
        Directory.CreateDirectory(invisibleDirectory);

        var firstProject = CreateClassLib("MyClassLib");
        var secondProject = CreateConsoleApp("MyConsoleApp");
        var thirdProject = CreateClassLib("MyClassLib2", null, invisibleDirectory);
        CreateDocument(firstProject, Path.Combine("Folder", "InnerProject.csproj"), "MyClassLib3");
        CreateDocument(secondProject, Path.Combine(".meteor", "InnerProject2.csproj"), "MyClassLib4");

        workspace.FindTargetsInWorkspace([MockProjectsDirectory]);
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
        var singleProject = CreateClassLib("MyClassLib");
        var multipleProject = CreateClassLib("MyClassLib2", MultiTargetFramework);
        var workspace = new TestWorkspace([singleProject, multipleProject]);
        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        // Create, SourceCode, SingleTFM
        var singleProjectSourceCodeDocumentPath = CreateDocument(singleProject, "Class2.cs", "class Class2 {}");
        workspace.CreateDocument(singleProjectSourceCodeDocumentPath);
        var singleProjectSourceCodeDocumentId = workspace.Solution!.GetDocumentIdsWithFilePath(singleProjectSourceCodeDocumentPath).Single();
        var singleProjectSourceCodeDocument = workspace.Solution.GetDocument(singleProjectSourceCodeDocumentId);
        Assert.Equal(singleProjectSourceCodeDocumentPath, singleProjectSourceCodeDocument!.FilePath);
        // Create, SourceCode, MultiTFM
        var multipleProjectSourceCodeDocumentPath = CreateDocument(multipleProject, "Class2.cs", "class Class2 {}");
        workspace.CreateDocument(multipleProjectSourceCodeDocumentPath);
        var multipleProjectSourceCodeDocumentIds = workspace.Solution!.GetDocumentIdsWithFilePath(multipleProjectSourceCodeDocumentPath);
        Assert.Equal(2, multipleProjectSourceCodeDocumentIds.Length);
        foreach (var multipleProjectSourceCodeDocumentId in multipleProjectSourceCodeDocumentIds) {
            var multipleProjectSourceCodeDocument = workspace.Solution.GetDocument(multipleProjectSourceCodeDocumentId);
            Assert.Equal(multipleProjectSourceCodeDocumentPath, multipleProjectSourceCodeDocument!.FilePath);
        }

        // Create, Text, SingleTFM
        var singleProjectTextDocumentPath = CreateDocument(singleProject, "Class2.xaml", "<Window />");
        workspace.CreateDocument(singleProjectTextDocumentPath);
        var singleProjectTextDocumentId = workspace.Solution!.GetAdditionalDocumentIdsWithFilePath(singleProjectTextDocumentPath).Single();
        var singleProjectTextDocument = workspace.Solution.GetAdditionalDocument(singleProjectTextDocumentId);
        Assert.Equal(singleProjectTextDocumentPath, singleProjectTextDocument!.FilePath);
        // Create, Text, MultiTFM
        var multipleProjectTextDocumentPath = CreateDocument(multipleProject, "Class2.xaml", "<Window />");
        workspace.CreateDocument(multipleProjectTextDocumentPath);
        var multipleProjectTextDocumentIds = workspace.Solution!.GetAdditionalDocumentIdsWithFilePath(multipleProjectTextDocumentPath);
        Assert.Equal(2, multipleProjectTextDocumentIds.Count());
        foreach (var multipleProjectTextDocumentId in multipleProjectTextDocumentIds) {
            var multipleProjectTextDocument = workspace.Solution.GetAdditionalDocument(multipleProjectTextDocumentId);
            Assert.Equal(multipleProjectTextDocumentPath, multipleProjectTextDocument!.FilePath);
        }

        // Update, SourceCode, SingleTFM
        workspace.UpdateDocument(singleProjectSourceCodeDocumentPath, "class Class2 { void Method() {}}");
        singleProjectSourceCodeDocumentId = workspace.Solution!.GetDocumentIdsWithFilePath(singleProjectSourceCodeDocumentPath).Single();
        singleProjectSourceCodeDocument = workspace.Solution.GetDocument(singleProjectSourceCodeDocumentId);
        var singleProjectSourceCodeDocumentContent = await singleProjectSourceCodeDocument!.GetTextAsync().ConfigureAwait(false);
        Assert.Equal("class Class2 { void Method() {}}", singleProjectSourceCodeDocumentContent.ToString());
        // Update, SourceCode, MultiTFM
        workspace.UpdateDocument(multipleProjectSourceCodeDocumentPath, "class Class2 { void Method() {}}");
        multipleProjectSourceCodeDocumentIds = workspace.Solution!.GetDocumentIdsWithFilePath(multipleProjectSourceCodeDocumentPath);
        Assert.Equal(2, multipleProjectSourceCodeDocumentIds.Length);
        foreach (var multipleProjectSourceCodeDocumentId in multipleProjectSourceCodeDocumentIds) {
            var multipleProjectSourceCodeDocument = workspace.Solution.GetDocument(multipleProjectSourceCodeDocumentId);
            var multipleProjectSourceCodeDocumentContent = await multipleProjectSourceCodeDocument!.GetTextAsync().ConfigureAwait(false);
            Assert.Equal("class Class2 { void Method() {}}", multipleProjectSourceCodeDocumentContent.ToString());
        }

        // Update, Text, SingleTFM
        workspace.UpdateDocument(singleProjectTextDocumentPath, "<Window x:Name=\"window\" />");
        singleProjectTextDocumentId = workspace.Solution!.GetAdditionalDocumentIdsWithFilePath(singleProjectTextDocumentPath).Single();
        singleProjectTextDocument = workspace.Solution.GetAdditionalDocument(singleProjectTextDocumentId);
        var singleProjectTextDocumentContent = await singleProjectTextDocument!.GetTextAsync().ConfigureAwait(false);
        Assert.Equal("<Window x:Name=\"window\" />", singleProjectTextDocumentContent.ToString());
        // Update, Text, MultiTFM
        workspace.UpdateDocument(multipleProjectTextDocumentPath, "<Window x:Name=\"window\" />");
        multipleProjectTextDocumentIds = workspace.Solution!.GetAdditionalDocumentIdsWithFilePath(multipleProjectTextDocumentPath);
        Assert.Equal(2, multipleProjectTextDocumentIds.Count());
        foreach (var multipleProjectTextDocumentId in multipleProjectTextDocumentIds) {
            var multipleProjectTextDocument = workspace.Solution.GetAdditionalDocument(multipleProjectTextDocumentId);
            var multipleProjectTextDocumentContent = await multipleProjectTextDocument!.GetTextAsync().ConfigureAwait(false);
            Assert.Equal("<Window x:Name=\"window\" />", multipleProjectTextDocumentContent.ToString());
        }

        // Delete, SourceCode, SingleTFM
        workspace.DeleteDocument(singleProjectSourceCodeDocumentPath);
        singleProjectSourceCodeDocumentId = workspace.Solution!.GetDocumentIdsWithFilePath(singleProjectSourceCodeDocumentPath).SingleOrDefault();
        Assert.Null(singleProjectSourceCodeDocumentId);
        // Delete, SourceCode, MultiTFM
        workspace.DeleteDocument(multipleProjectSourceCodeDocumentPath);
        multipleProjectSourceCodeDocumentIds = workspace.Solution!.GetDocumentIdsWithFilePath(multipleProjectSourceCodeDocumentPath);
        Assert.Empty(multipleProjectSourceCodeDocumentIds);

        // Delete, Text, SingleTFM
        workspace.DeleteDocument(singleProjectTextDocumentPath);
        singleProjectTextDocumentId = workspace.Solution!.GetAdditionalDocumentIdsWithFilePath(singleProjectTextDocumentPath).SingleOrDefault();
        Assert.Null(singleProjectTextDocumentId);
        // Delete, Text, MultiTFM
        workspace.DeleteDocument(multipleProjectTextDocumentPath);
        multipleProjectTextDocumentIds = workspace.Solution!.GetAdditionalDocumentIdsWithFilePath(multipleProjectTextDocumentPath);
        Assert.Empty(multipleProjectTextDocumentIds);

        // DeleteFolder, SourceCode, SingleTFM
        var folderFiles = new List<string>();
        for (int i = 1; i < 5; i++) {
            folderFiles.Add(CreateDocument(singleProject, Path.Combine("TestFolder", $"Class_{i}.cs"), $"class Class_{i} {{}}"));
            workspace.CreateDocument(folderFiles[i - 1]);
        }
        Assert.Equal(folderFiles.Count, workspace.Solution.GetDocumentIdsWithFolderPath(Path.GetDirectoryName(folderFiles.First())!).Count());
        workspace.DeleteFolder(Path.GetDirectoryName(folderFiles.First())!);
        foreach (var folderFile in folderFiles)
            Assert.Empty(workspace.Solution.GetDocumentIdsWithFilePath(folderFile));
        // DeleteFolder, SourceCode, MultiTFM
        folderFiles.Clear();
        for (int i = 1; i < 5; i++) {
            folderFiles.Add(CreateDocument(multipleProject, Path.Combine("TestFolder", $"Class_{i}.cs"), $"class Class_{i} {{}}"));
            workspace.CreateDocument(folderFiles[i - 1]);
        }
        Assert.Equal(folderFiles.Count * 2, workspace.Solution.GetDocumentIdsWithFolderPath(Path.GetDirectoryName(folderFiles.First())!).Count());
        workspace.DeleteFolder(Path.GetDirectoryName(folderFiles.First())!);
        foreach (var folderFile in folderFiles)
            Assert.Empty(workspace.Solution.GetDocumentIdsWithFilePath(folderFile));

        // DeleteFolder, Text, SingleTFM
        folderFiles.Clear();
        for (int i = 1; i < 5; i++) {
            folderFiles.Add(CreateDocument(singleProject, Path.Combine("TestFolder", $"File_{i}.xaml"), $"<Window{i} />"));
            workspace.CreateDocument(folderFiles[i - 1]);
        }
        Assert.Equal(folderFiles.Count, workspace.Solution.GetAdditionalDocumentIdsWithFolderPath(Path.GetDirectoryName(folderFiles.First())!).Count());
        workspace.DeleteFolder(Path.GetDirectoryName(folderFiles.First())!);
        foreach (var folderFile in folderFiles)
            Assert.Empty(workspace.Solution.GetAdditionalDocumentIdsWithFilePath(folderFile));
        // DeleteFolder, Text, MultiTFM
        folderFiles.Clear();
        for (int i = 1; i < 5; i++) {
            folderFiles.Add(CreateDocument(multipleProject, Path.Combine("TestFolder", $"File{i}.xaml"), $"File_{i}.xaml"));
            workspace.CreateDocument(folderFiles[i - 1]);
        }
        Assert.Equal(folderFiles.Count * 2, workspace.Solution.GetAdditionalDocumentIdsWithFolderPath(Path.GetDirectoryName(folderFiles.First())!).Count());
        workspace.DeleteFolder(Path.GetDirectoryName(folderFiles.First())!);
        foreach (var folderFile in folderFiles)
            Assert.Empty(workspace.Solution.GetAdditionalDocumentIdsWithFilePath(folderFile));
    }

    [Fact]
    public async Task SolutionChangesInIntermidiatePathTest() {
        var projectPath = CreateClassLib("MyClassLib", MultiTargetFramework);
        var workspace = new TestWorkspace([projectPath]);
        await workspace.LoadSolutionAsync(CancellationToken.None).ConfigureAwait(false);

        var documentPath = CreateDocument(projectPath, Path.Combine("obj", "Class2.cs"), "class Class2 {}");
        workspace.CreateDocument(documentPath);
        var documentIds = workspace.Solution!.GetDocumentIdsWithFilePath(documentPath);
        Assert.Equal(2, documentIds.Length);

        workspace.UpdateDocument(documentPath, "class Class2 { void Method() {}}");
        foreach (var documentId in documentIds) {
            var document = workspace.Solution.GetDocument(documentId);
            Assert.Equal(documentPath, document!.FilePath);
        }
    }

    public void Dispose() {
        DeleteMockData();
    }
}