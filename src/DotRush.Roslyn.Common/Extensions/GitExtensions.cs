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

        return IsLockedState(gitPath) 
            || IsMergeState(gitPath)
            || IsRebaseState(gitPath);
    }

    public static bool IsMergeState(string gitPath) {
        var mergeHeadPath = Path.Combine(gitPath, "MERGE_HEAD");
        return File.Exists(mergeHeadPath);
    }
    public static bool IsRebaseState(string gitPath) {
        var rebaseMergePath = Path.Combine(gitPath, "rebase-merge");
        var rebaseHeadPath = Path.Combine(gitPath, "REBASE_HEAD");
        return Directory.Exists(rebaseMergePath) || File.Exists(rebaseHeadPath);
    }
    public static bool IsLockedState(string gitPath) {
        var lockPath = Path.Combine(gitPath, "index.lock");
        return File.Exists(lockPath);
    }
}