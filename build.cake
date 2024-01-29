#addin nuget:?package=Cake.FileHelpers&version=6.1.2

using _Path = System.IO.Path;

string version;
string runtime = Argument<string>("arch", "osx-arm64");
string target = Argument<string>("target", "vsix");
string configuration = Argument<string>("configuration", "debug");

public string RootDirectory => MakeAbsolute(Directory(".")).ToString();
public string ArtifactsDirectory => _Path.Combine(RootDirectory, "artifacts");
public string ServerProjectFilePath => _Path.Combine(RootDirectory, "src", "DotRush.Server", "DotRush.csproj");
public string ExtensionStagingDirectory => _Path.Combine(RootDirectory, "extension");
public string BinariesDirectory => _Path.Combine(ExtensionStagingDirectory, "bin");


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

Task("server").Does(() => DotNetPublish(ServerProjectFilePath, new DotNetPublishSettings {
	MSBuildSettings = new DotNetMSBuildSettings { AssemblyVersion = version },
	Configuration = configuration,
	Runtime = runtime
}));

Task("vsix")
	.IsDependentOn("clean")
	.IsDependentOn("server")
	.DoesForEach<FilePath>(GetFiles("*.json"), file => {
		var regex = @"^\s\s(""version"":\s+)("".+"")(,)";
		var options = System.Text.RegularExpressions.RegexOptions.Multiline;
		ReplaceRegexInFiles(file.ToString(), regex, $"  $1\"{version}\"$3", options);
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


RunTarget(target);

void ExecuteCommand(string command, string arguments) {
	if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
		arguments = $"/c \"{command} {arguments}\"";
		command = "cmd";
	}
	StartProcess(command, arguments);
}