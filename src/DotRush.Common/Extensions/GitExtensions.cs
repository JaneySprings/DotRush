namespace DotRush.Common.Extensions;

public static class GitExtensions {
    public static string? GetRepositoryFolder(string path) {
        var directory = new DirectoryInfo(path);
        while (directory != null) {
            var gitFolder = directory.GetDirectories(".git").FirstOrDefault();
            if (gitFolder != null)
                return gitFolder.FullName;

            var gitFile = directory.GetFiles(".git").FirstOrDefault();
            if (gitFile != null)
                return GetRepositoryFromFile(gitFile.FullName);

            directory = directory.Parent;
        }

        return null;
    }
    public static string? GetRepositoryFolder(IEnumerable<string> paths) {
        foreach (var path in paths) {
            var gitFolder = GetRepositoryFolder(path);
            if (gitFolder != null)
                return gitFolder;
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

    private static bool IsMergeState(string gitPath) {
        var mergeHeadPath = Path.Combine(gitPath, "MERGE_HEAD");
        return File.Exists(mergeHeadPath);
    }
    private static bool IsRebaseState(string gitPath) {
        var rebaseMergePath = Path.Combine(gitPath, "rebase-merge");
        // var rebaseHeadPath = Path.Combine(gitPath, "REBASE_HEAD");
        return Directory.Exists(rebaseMergePath); //|| File.Exists(rebaseHeadPath);
    }
    private static bool IsLockedState(string gitPath) {
        var lockPath = Path.Combine(gitPath, "index.lock");
        return File.Exists(lockPath);
    }
    private static string? GetRepositoryFromFile(string gitFile) {
        var gitPath = File.ReadAllText(gitFile).Trim();
        if (gitPath.StartsWith("gitdir: ", StringComparison.OrdinalIgnoreCase))
            return gitPath.Substring(8).ToPlatformPath();

        return null;
    }
}