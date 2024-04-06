using Microsoft.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.WorkDone;

namespace DotRush.Roslyn.Server.Extensions;

public static class ProjectExtensions {

    public static string GetTargetFramework(this Project project) {
        var frameworkStartIndex = project.Name.LastIndexOf('(');
        if (frameworkStartIndex == -1)
            return string.Empty;

        return project.Name.Substring(frameworkStartIndex + 1, project.Name.Length - frameworkStartIndex - 2);
    }
    public static string GetOutputPath(this Project project) {
        return FirstFolderOrDefault(project.FilePath, project.OutputFilePath, $"bin{Path.DirectorySeparatorChar}");
    }
    public static string GetIntermediateOutputPath(this Project project) {
        return FirstFolderOrDefault(project.FilePath, project.OutputRefFilePath, $"obj{Path.DirectorySeparatorChar}");
    }
    public static async Task CompileAsync(this Project project, IWorkDoneObserver? observer, CancellationToken cancellationToken) {
        var projectName = Path.GetFileNameWithoutExtension(project.FilePath);
        observer?.OnNext(new WorkDoneProgressReport { Message = string.Format(Resources.MessageProjectCompile, projectName) });
        _ = await project.GetCompilationAsync(cancellationToken);
    }

    private static string FirstFolderOrDefault(string? projectPath, string? targetPath, string fallbackFolder) {
        var projectDirectory = Path.GetDirectoryName(projectPath) + Path.DirectorySeparatorChar;
        if (targetPath == null || !targetPath.StartsWith(projectDirectory))
            return Path.Combine(projectDirectory, fallbackFolder);

        var relativePath = targetPath.Replace(projectDirectory, string.Empty);
        if (string.IsNullOrEmpty(relativePath))
            return Path.Combine(projectDirectory, fallbackFolder);

        return projectDirectory + relativePath.Split(Path.DirectorySeparatorChar).First() + Path.DirectorySeparatorChar;
    }
}