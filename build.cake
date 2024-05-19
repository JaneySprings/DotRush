#addin nuget:?package=Cake.FileHelpers&version=7.0.0

using _Path = System.IO.Path;

public string RootDirectory => MakeAbsolute(Directory("./")).ToString();
public string ArtifactsDirectory => _Path.Combine(RootDirectory, "artifacts");
public string ExtensionStagingDirectory => _Path.Combine(RootDirectory, "extension");
public string ExtensionBinariesDirectory => _Path.Combine(ExtensionStagingDirectory, "bin");

public string DotRushServerProjectPath => _Path.Combine(RootDirectory, "src", "DotRush.Roslyn.Server", "DotRush.Roslyn.Server.csproj");
public string DotRushTestsProjectPath => _Path.Combine(RootDirectory, "src", "DotRush.Roslyn.Tests", "DotRush.Roslyn.Tests.csproj");

var target = Argument("target", "vsix");
var runtime = Argument("arch", "osx-arm64");
var version = Argument("release-version", "1.0.0");
var configuration = Argument("configuration", "debug");

///////////////////////////////////////////////////////////////////////////////
// COMMON
///////////////////////////////////////////////////////////////////////////////

Setup(context => {
	var date = DateTime.Now;
	version = $"{DateTime.Now.ToString("yy")}.{date.ToString("%M")}.{date.DayOfYear}";
	EnsureDirectoryExists(ArtifactsDirectory);
});

Task("clean").Does(() => {
	EnsureDirectoryExists(ArtifactsDirectory);
	CleanDirectory(ExtensionStagingDirectory);
	CleanDirectories(_Path.Combine(RootDirectory, "src", "**", "bin"));
	CleanDirectories(_Path.Combine(RootDirectory, "src", "**", "obj"));
});

///////////////////////////////////////////////////////////////////////////////
// DOTNET
///////////////////////////////////////////////////////////////////////////////

Task("server").Does(() => DotNetPublish(DotRushServerProjectPath, new DotNetPublishSettings {
	MSBuildSettings = new DotNetMSBuildSettings { AssemblyVersion = version },
	Configuration = configuration,
	Runtime = runtime,
}));

Task("test").Does(() => DotNetTest(DotRushTestsProjectPath, new DotNetTestSettings {  
	Configuration = configuration,
	Verbosity = DotNetVerbosity.Quiet,
	ResultsDirectory = ArtifactsDirectory,
	Loggers = new[] { "trx" }
}));


///////////////////////////////////////////////////////////////////////////////
// TYPESCRIPT
///////////////////////////////////////////////////////////////////////////////

Task("vsix")
	.IsDependentOn("clean")
	.IsDependentOn("server")
	.Does(() => {
		var package = _Path.Combine(RootDirectory, "package.json");
		var regex = @"^\s\s(""version"":\s+)("".+"")(,)";
		var options = System.Text.RegularExpressions.RegexOptions.Multiline;
		ReplaceRegexInFiles(package, regex, $"  $1\"{version}\"$3", options);
	})
	.Does(() => {
		switch (runtime) {
			case "win-x64": runtime = "win32-x64"; break;
			case "win-arm64": runtime = "win32-arm64"; break;
			case "osx-x64": runtime = "darwin-x64"; break;
			case "osx-arm64": runtime = "darwin-arm64"; break;
		}
		var output = _Path.Combine(ArtifactsDirectory, $"DotRush.v{version}_{runtime}.vsix");
		ExecuteCommand("vsce", $"package --target {runtime} --out {output}");
	});


void ExecuteCommand(string command, string arguments) {
	if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
		arguments = $"/c \"{command} {arguments}\"";
		command = "cmd";
	}
	if (StartProcess(command, arguments) != 0)
		throw new Exception("Command exited with non-zero exit code.");
}

RunTarget(target);