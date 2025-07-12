namespace DotRush.Common.Interop.Android;

public static class AndroidSdkLocator {
    public static string? UnityEditorPath { get; set; }

    public static string SdkLocation() {
        var path = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        path = Environment.GetEnvironmentVariable("ANDROID_HOME");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        // Try to find the SDK path in the default AndroidStudio locations
        if (RuntimeInfo.IsWindows)
            path = Path.Combine(RuntimeInfo.HomeDirectory, "AppData", "Local", "Android", "Sdk");
        else if (RuntimeInfo.IsMacOS)
            path = Path.Combine(RuntimeInfo.HomeDirectory, "Library", "Android", "Sdk");
        else
            path = Path.Combine(RuntimeInfo.HomeDirectory, "Android", "Sdk");

        if (Directory.Exists(path))
            return path;

        // Try to find the SDK path in the default VisualStudio locations
        if (RuntimeInfo.IsWindows)
            path = Path.Combine(RuntimeInfo.ProgramX86Directory, "Android", "android-sdk");
        else if (RuntimeInfo.IsMacOS)
            path = Path.Combine(RuntimeInfo.HomeDirectory, "Library", "Developer", "Xamarin", "android-sdk-macosx");

        if (Directory.Exists(path))
            return path;

        // Try to find the SDK path in the Unity Editor locations
        if (!string.IsNullOrEmpty(UnityEditorPath)) {
            if (RuntimeInfo.IsWindows)
                path = Path.Combine(UnityEditorPath, "..", "Data", "PlaybackEngines", "AndroidPlayer", "SDK");
            else //TODO: Check Linux path
                path = Path.Combine(UnityEditorPath, "..", "PlaybackEngines", "AndroidPlayer", "SDK");
        }

        if (Directory.Exists(path))
            return path;

        return string.Empty;
    }
    public static string AvdLocation() {
        var path = Environment.GetEnvironmentVariable("ANDROID_AVD_HOME");
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            return path;

        return Path.Combine(RuntimeInfo.HomeDirectory, ".android", "avd");
    }

    public static FileInfo AdbTool() {
        string sdk = AndroidSdkLocator.SdkLocation();
        string path = Path.Combine(sdk, "platform-tools", "adb" + RuntimeInfo.ExecExtension);

        if (!File.Exists(path))
            throw new FileNotFoundException("Could not find adb tool");

        return new FileInfo(path);
    }
    public static FileInfo EmulatorTool() {
        string sdk = AndroidSdkLocator.SdkLocation();
        string path = Path.Combine(sdk, "emulator", "emulator" + RuntimeInfo.ExecExtension);

        if (!File.Exists(path))
            throw new FileNotFoundException("Could not find emulator tool");

        return new FileInfo(path);
    }
    public static FileInfo AvdTool() {
        string sdk = AndroidSdkLocator.SdkLocation();
        string tools = Path.Combine(sdk, "cmdline-tools");
        FileInfo? newestTool = null;

        foreach (string directory in Directory.GetDirectories(tools)) {
            string avdPath = Path.Combine(directory, "bin", "avdmanager" + RuntimeInfo.ExecExtension);

            if (File.Exists(avdPath)) {
                var tool = new FileInfo(avdPath);

                if (newestTool == null || tool.CreationTime > newestTool.CreationTime)
                    newestTool = tool;
            }
        }

        if (newestTool == null || !newestTool.Exists)
            throw new FileNotFoundException("Could not find avdmanager tool");

        return newestTool;
    }
}