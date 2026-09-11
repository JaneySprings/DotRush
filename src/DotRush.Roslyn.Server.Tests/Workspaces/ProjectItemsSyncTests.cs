using DotRush.Roslyn.Workspaces.Extensions;
using DotRush.Roslyn.Workspaces.FileSystem;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace DotRush.Roslyn.Server.Tests;

/// <summary>
/// New files are attached to projects exactly as MSBuild would include them: Compile/AdditionalFiles globs,
/// explicit includes, Remove elements, conditions per target framework and item references.
/// </summary>
public class ProjectItemsSyncTests : SimpleWorkspaceFixture {

    [Test]
    public async Task CustomCompileGlobTest() {
        var projectPath = CreateProjectWithItems("MyProject", "net10.0", @"
            <PropertyGroup>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
            </PropertyGroup>
            <ItemGroup>
                <Compile Include=""**/*.my.cs"" />
            </ItemGroup>");
        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);

        var includedPath = CreateAndAddFile(projectPath, "Included.my.cs");
        var excludedPath = CreateAndAddFile(projectPath, "Excluded.cs");
        var nestedPath = CreateAndAddFile(projectPath, Path.Combine("Nested", "Deep.my.cs"));

        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(includedPath).ToArray(), Has.Length.EqualTo(1));
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(nestedPath).ToArray(), Has.Length.EqualTo(1));
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(excludedPath), Is.Empty);
    }

    [Test]
    public async Task DisabledDefaultItemsWithNonRecursiveGlobTest() {
        var projectPath = CreateProjectWithItems("MyProject", "net10.0", @"
            <PropertyGroup>
                <EnableDefaultItems>false</EnableDefaultItems>
            </PropertyGroup>
            <ItemGroup>
                <Compile Include=""*.my.cs"" />
            </ItemGroup>");
        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);

        var includedPath = CreateAndAddFile(projectPath, "Included.my.cs");
        var plainPath = CreateAndAddFile(projectPath, "Plain.cs");
        var nestedPath = CreateAndAddFile(projectPath, Path.Combine("Nested", "Deep.my.cs"));
        // Same as the file watcher reports a batch of created files
        var watchedPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "Watched.cs");
        var watchedIncludedPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "Watched.my.cs");
        File.WriteAllText(watchedPath, "public class Watched {}");
        File.WriteAllText(watchedIncludedPath, "public class WatchedIncluded {}");
        ((IWorkspaceChangeListener)Workspace).OnDocumentsCreated(new[] { watchedPath, watchedIncludedPath });

        var project = Workspace.Solution!.Projects.Single();
        var userDocuments = project.Documents.Select(d => d.FilePath!).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")).Select(Path.GetFileName).ToArray();
        Assert.That(userDocuments, Is.EquivalentTo(new[] { "Included.my.cs", "Watched.my.cs" }));
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(plainPath), Is.Empty);
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(nestedPath), Is.Empty, "The glob is not recursive");
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(watchedPath), Is.Empty);
    }

    [Test]
    public async Task CompileRemoveTest() {
        var projectPath = CreateProjectWithItems("MyProject", "net10.0", @"
            <ItemGroup>
                <Compile Remove=""Excluded/**"" />
            </ItemGroup>");
        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);

        var includedPath = CreateAndAddFile(projectPath, Path.Combine("Included", "File.cs"));
        var excludedPath = CreateAndAddFile(projectPath, Path.Combine("Excluded", "File.cs"));

        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(includedPath).ToArray(), Has.Length.EqualTo(1));
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(excludedPath), Is.Empty);
    }

    [Test]
    public async Task ConditionalIncludePerTargetFrameworkTest() {
        // Same shape as the MAUI SDK: platform folders are removed and re-included for the matching target framework only
        var projectPath = CreateProjectWithItems("MyProject", "net8.0;net10.0", @"
            <ItemGroup>
                <Compile Remove=""Platforms/**"" />
                <Compile Include=""Platforms/Android/**/*.cs"" Condition=""'$(TargetFramework)' == 'net8.0'"" />
                <Compile Include=""Platforms/iOS/**/*.cs"" Condition=""'$(TargetFramework)' == 'net10.0'"" />
            </ItemGroup>");
        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);
        Assert.That(Workspace.Solution!.Projects.Count(), Is.EqualTo(2));

        var androidPath = CreateAndAddFile(projectPath, Path.Combine("Platforms", "Android", "MainActivity.cs"));
        var iosPath = CreateAndAddFile(projectPath, Path.Combine("Platforms", "iOS", "AppDelegate.cs"));
        var windowsPath = CreateAndAddFile(projectPath, Path.Combine("Platforms", "Windows", "App.cs"));
        var sharedPath = CreateAndAddFile(projectPath, "Shared.cs");

        var androidDocuments = Workspace.Solution!.GetDocuments(Workspace.Solution!.GetDocumentIdsWithFilePathV2(androidPath));
        Assert.That(androidDocuments, Has.Length.EqualTo(1));
        Assert.That(androidDocuments[0].Project.Name, Is.EqualTo("MyProject(net8.0)"));

        var iosDocuments = Workspace.Solution!.GetDocuments(Workspace.Solution!.GetDocumentIdsWithFilePathV2(iosPath));
        Assert.That(iosDocuments, Has.Length.EqualTo(1));
        Assert.That(iosDocuments[0].Project.Name, Is.EqualTo("MyProject(net10.0)"));

        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(windowsPath), Is.Empty);
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(sharedPath).ToArray(), Has.Length.EqualTo(2));
    }

    [Test]
    public async Task ItemsAdjustedByTargetsTest() {
        // Same mechanism as the MAUI SDK: the other platforms' folders are removed by a target, not during evaluation
        var projectPath = CreateProjectWithItems("MyProject", "net8.0;net10.0", @"
            <Target Name=""RemoveOtherPlatforms"" BeforeTargets=""CoreCompile"">
                <ItemGroup>
                    <Compile Remove=""Platforms/iOS/**"" Condition=""'$(TargetFramework)' != 'net8.0'"" />
                    <Compile Remove=""Platforms/MacCatalyst/**"" Condition=""'$(TargetFramework)' != 'net10.0'"" />
                </ItemGroup>
            </Target>");
        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);

        var iosPath = CreateAndAddFile(projectPath, Path.Combine("Platforms", "iOS", "AppDelegate.cs"));
        var catalystPath = CreateAndAddFile(projectPath, Path.Combine("Platforms", "MacCatalyst", "AppDelegate.cs"));

        var iosDocuments = Workspace.Solution!.GetDocuments(Workspace.Solution!.GetDocumentIdsWithFilePathV2(iosPath));
        Assert.That(iosDocuments.Select(d => d.Project.Name), Is.EquivalentTo(new[] { "MyProject(net8.0)" }));
        var catalystDocuments = Workspace.Solution!.GetDocuments(Workspace.Solution!.GetDocumentIdsWithFilePathV2(catalystPath));
        Assert.That(catalystDocuments.Select(d => d.Project.Name), Is.EquivalentTo(new[] { "MyProject(net10.0)" }));
    }

    [Test]
    public async Task ExplicitIncludeOfMissingFileTest() {
        var projectPath = CreateProjectWithItems("MyProject", "net10.0", @"
            <PropertyGroup>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
            </PropertyGroup>
            <ItemGroup>
                <Compile Include=""Later.cs"" />
            </ItemGroup>");
        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);

        var laterPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "Later.cs");
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(laterPath), Is.Empty, "Missing files should not be loaded");

        CreateAndAddFile(projectPath, "Later.cs");
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(laterPath).ToArray(), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task AdditionalFilesViaItemReferenceTest() {
        // Same shape as the MAUI SDK: <AdditionalFiles Include="@(MauiXaml)" /> where MauiXaml is a glob
        var projectPath = CreateProjectWithItems("MyProject", "net10.0", @"
            <ItemGroup>
                <MyXaml Include=""**/*.xaml"" />
                <AdditionalFiles Include=""@(MyXaml)"" />
            </ItemGroup>");
        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);

        var xamlPath = CreateAndAddFile(projectPath, Path.Combine("Views", "MainPage.xaml"), "<Page />");
        var jsonPath = CreateAndAddFile(projectPath, "data.json", "{}");

        Assert.That(Workspace.Solution!.GetAdditionalDocumentIdsWithFilePathV2(xamlPath).ToArray(), Has.Length.EqualTo(1));
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(xamlPath), Is.Empty);
        Assert.That(Workspace.Solution!.GetAdditionalDocumentIdsWithFilePathV2(jsonPath), Is.Empty);
    }

    [Test]
    public async Task DeleteKeepsGeneratedDocumentsTest() {
        var projectPath = CreateProjectWithItems("MyProject", "net10.0", string.Empty);
        await Workspace.LoadAsync(new[] { projectPath }, CancellationToken.None).ConfigureAwait(false);

        var project = Workspace.Solution!.Projects.Single();
        var generatedDocument = project.Documents.FirstOrDefault(d => d.FilePath != null && d.FilePath.StartsWith(project.GetIntermediateOutputPath(), StringComparison.OrdinalIgnoreCase));
        Assert.That(generatedDocument, Is.Not.Null, "Expected build generated documents (AssemblyInfo, GlobalUsings) in obj");

        Workspace.DeleteDocument(generatedDocument!.FilePath!);
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(generatedDocument.FilePath!).ToArray(), Has.Length.EqualTo(1), "Generated documents are recreated by the next build and must survive deletion");

        var userPath = CreateAndAddFile(projectPath, "User.cs");
        Workspace.DeleteDocument(userPath);
        Assert.That(Workspace.Solution!.GetDocumentIdsWithFilePathV2(userPath), Is.Empty);
    }

    private string CreateProjectWithItems(string name, string targetFrameworks, string projectBody) {
        var projectDirectory = Path.Combine(SandboxDirectory, name);
        Directory.CreateDirectory(projectDirectory);

        var projectFile = Path.Combine(projectDirectory, $"{name}.csproj");
        File.WriteAllText(projectFile, $@"<Project Sdk=""Microsoft.NET.Sdk"">
            <PropertyGroup>
                <OutputType>Library</OutputType>
                <TargetFrameworks>{targetFrameworks}</TargetFrameworks>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
            </PropertyGroup>
            {projectBody}
        </Project>");
        return projectFile;
    }
    private string CreateAndAddFile(string projectPath, string relativePath, string content = "public class TestFile {}") {
        var filePath = Path.Combine(Path.GetDirectoryName(projectPath)!, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content);
        Workspace.CreateDocument(filePath);
        return filePath;
    }
}
