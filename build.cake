using _Path = System.IO.Path;

public string RootDirectory => MakeAbsolute(Directory("./")).ToString();
public string ArtifactsDirectory => _Path.Combine(RootDirectory, "artifacts");

public string DotRushServerProjectPath => _Path.Combine(RootDirectory, "src", "DotRush.Roslyn.Server", "DotRush.Roslyn.Server.csproj");
public string DotRushTestsProjectPath => _Path.Combine(RootDirectory, "src", "DotRush.Roslyn.Tests", "DotRush.Roslyn.Tests.csproj");
public string DotRushExtensionVSCodePath => _Path.Combine(RootDirectory, "src", "DotRush.Extension.VSCode");

var target = Argument("target", "vsix");
var runtime = Argument("arch", "osx-arm64");
var version = Argument("release-version", "1.0.0");
var configuration = Argument("configuration", "debug");


Setup(context => {
	var date = DateTime.Now;
	version = $"{DateTime.Now.ToString("yy")}.{date.ToString("%M")}.{date.DayOfYear}";
	EnsureDirectoryExists(ArtifactsDirectory);
});

Task("clean").Does(() => {
	EnsureDirectoryExists(ArtifactsDirectory);
	CleanDirectories(_Path.Combine(RootDirectory, "src", "DotRush.Roslyn.*", "bin"));
	CleanDirectories(_Path.Combine(RootDirectory, "src", "DotRush.Roslyn.*", "obj"));
	CleanDirectories(_Path.Combine(DotRushExtensionVSCodePath, "extension"));
});

Task("server")
	.IsDependentOn("clean")
	.Does(() => DotNetPublish(DotRushServerProjectPath, new DotNetPublishSettings {
		MSBuildSettings = new DotNetMSBuildSettings { AssemblyVersion = version },
		OutputDirectory = _Path.Combine(ArtifactsDirectory, configuration),
		Configuration = configuration,
		Runtime = runtime,
	}))
	.Does(() => {
		var input = _Path.Combine(ArtifactsDirectory, configuration);
		var output = _Path.Combine(ArtifactsDirectory, $"DotRush.Roslyn.Server.v{version}_{runtime}.zip");
		Zip(input, output);
		DeleteDirectory(input, new DeleteDirectorySettings { Recursive = true });
	});

Task("test")
	.IsDependentOn("clean")
	.Does(() => DotNetTest(DotRushTestsProjectPath, new DotNetTestSettings {  
		Configuration = configuration,
		Verbosity = DotNetVerbosity.Quiet,
		ResultsDirectory = ArtifactsDirectory,
		Loggers = new[] { "trx" }
	}));

Task("vsix")
	.IsDependentOn("clean")
	.Does(() => DotNetPublish(DotRushServerProjectPath, new DotNetPublishSettings {
		MSBuildSettings = new DotNetMSBuildSettings { AssemblyVersion = version },
		OutputDirectory = _Path.Combine(DotRushExtensionVSCodePath, "extension", "bin"),
		Configuration = configuration,
		Runtime = runtime,
	}))
	.Does(() => {
		switch (runtime) {
			case "win-x64": runtime = "win32-x64"; break;
			case "win-arm64": runtime = "win32-arm64"; break;
			case "osx-x64": runtime = "darwin-x64"; break;
			case "osx-arm64": runtime = "darwin-arm64"; break;
		}
		var output = _Path.Combine(ArtifactsDirectory, $"DotRush.v{version}_{runtime}.vsix");
		CopyFile(_Path.Combine(RootDirectory, "README.md"), _Path.Combine(DotRushExtensionVSCodePath, "README.md"));
		CopyFile(_Path.Combine(RootDirectory, "LICENSE"), _Path.Combine(DotRushExtensionVSCodePath, "LICENSE"));
		CopyDirectory(_Path.Combine(RootDirectory, "assets"), _Path.Combine(DotRushExtensionVSCodePath, "assets"));
		ExecuteCommand("vsce", $"package --target {runtime} --out {output} {version}", DotRushExtensionVSCodePath);
	});


void ExecuteCommand(string command, string arguments, string cwd) {
	if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
		arguments = $"/c \"{command} {arguments}\"";
		command = "cmd";
	}
	if (StartProcess(command, new ProcessSettings { Arguments = arguments, WorkingDirectory = cwd }) != 0)
		throw new Exception("Command exited with non-zero exit code.");
}

RunTarget(target);