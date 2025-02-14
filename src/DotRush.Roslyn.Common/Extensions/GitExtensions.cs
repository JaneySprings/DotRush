namespace DotRush.Roslyn.Common.Extensions;

public static class GitExtensions {
    public static string? GetRepositoryFolder(string path) {
        var directory = new DirectoryInfo(path);
        while (directory != null) {
            var gitFolder = directory.GetDirectories(".git").FirstOrDefault();
            if (gitFolder != null)
                return gitFolder.FullName;

            directory = directory.Parent;
        }

        return null;
    }
    public static bool IsRepositoryLocked(string? gitPath) {
        if (string.IsNullOrEmpty(gitPath))
            return false;

        return GitExtensions.IsMergeState(gitPath) 
            || GitExtensions.IsRebaseState(gitPath) 
            || GitExtensions.IsCheckoutState(gitPath);
    }

    public static bool IsMergeState(string gitPath) {
        var mergeHeadPath = Path.Combine(gitPath, "MERGE_HEAD");
        return File.Exists(mergeHeadPath);
    }
    public static bool IsRebaseState(string gitPath) {
        var rebaseApplyPath = Path.Combine(gitPath, "rebase-apply");
        return Directory.Exists(rebaseApplyPath);
    }
    public static bool IsCheckoutState(string gitPath) {
        var headPath = Path.Combine(gitPath, "HEAD");
        return File.Exists(headPath);
    }
}