using System.Runtime.InteropServices;
using _Path = System.IO.Path;

public string RootDirectory => MakeAbsolute(Directory("./")).ToString();
public string ArtifactsDirectory => _Path.Combine(RootDirectory, "artifacts");
public string VSCodeExtensionDirectory => _Path.Combine(RootDirectory, "src", "VSCode", "extension");

var target = Argument("target", "vsix");
var version = Argument("release-version", "1.0.0");
var configuration = Argument("configuration", "debug");
var runtime = Argument("arch", RuntimeInformation.RuntimeIdentifier);


Setup(context => {
	var date = DateTime.Now;
	version = $"{DateTime.Now.ToString("yy")}.{date.ToString("%M")}.{date.DayOfYear}";
	EnsureDirectoryExists(ArtifactsDirectory);
});

Task("clean").Does(() => {
	EnsureDirectoryExists(ArtifactsDirectory);
	CleanDirectories(_Path.Combine(RootDirectory, "src", "DotRush.*", "**", "bin"));
	CleanDirectories(_Path.Combine(RootDirectory, "src", "DotRush.*", "**", "obj"));
	CleanDirectories(VSCodeExtensionDirectory);
});

Task("server")
	.Does(() => DotNetPublish(_Path.Combine(RootDirectory, "src", "DotRush.Roslyn.Server", "DotRush.Roslyn.Server.csproj"), new DotNetPublishSettings {
		MSBuildSettings = new DotNetMSBuildSettings { AssemblyVersion = version },
		OutputDirectory = _Path.Combine(VSCodeExtensionDirectory, "bin", "LanguageServer"),
		Configuration = configuration,
		Runtime = runtime,
	}))
	.Does(() => {
		var input = _Path.Combine(VSCodeExtensionDirectory, "bin", "LanguageServer");
		var output = _Path.Combine(ArtifactsDirectory, $"DotRush.Roslyn.Server.v{version}_{runtime}.zip");
		Zip(input, output);
	});

Task("workspaces")
	.Does(() => DotNetPublish(_Path.Combine(RootDirectory, "src", "DotRush.Essentials.Workspaces", "DotRush.Essentials.Workspaces.csproj"), new DotNetPublishSettings {
		MSBuildSettings = new DotNetMSBuildSettings { AssemblyVersion = version },
		OutputDirectory = _Path.Combine(VSCodeExtensionDirectory, "bin", "Workspaces"),
		Configuration = configuration,
		Runtime = runtime,
	}));

Task("test")
	.IsDependentOn("clean")
	.Does(() => DotNetTest(_Path.Combine(RootDirectory, "src", "DotRush.Roslyn.Tests", "DotRush.Roslyn.Tests.csproj"), new DotNetTestSettings {  
		Configuration = configuration,
		Verbosity = DotNetVerbosity.Quiet,
		ResultsDirectory = ArtifactsDirectory,
		Loggers = new[] { "trx" }
	}));

Task("vsix")
	.IsDependentOn("clean")
	.IsDependentOn("server")
	.IsDependentOn("workspaces")
	.Does(() => {
		var vsruntime = runtime.Replace("win-", "win32-").Replace("osx-", "darwin-");
		var output = _Path.Combine(ArtifactsDirectory, $"DotRush.v{version}_{vsruntime}.vsix");
		var extensionPath = _Path.Combine(VSCodeExtensionDirectory, "..");
		ExecuteCommand("npm", "install", extensionPath);
		ExecuteCommand("vsce", $"package --target {vsruntime} --out {output} --no-git-tag-version {version}", extensionPath);
	});


void ExecuteCommand(string command, string arguments, string? cwd = null) {
	if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
		arguments = $"/c \"{command} {arguments}\"";
		command = "cmd";
	}
	if (StartProcess(command, new ProcessSettings { WorkingDirectory = cwd, Arguments = arguments }) != 0)
		throw new Exception("Command exited with non-zero exit code.");
}

RunTarget(target);