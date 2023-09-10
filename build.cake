#addin nuget:?package=Cake.FileHelpers&version=6.1.2
#addin nuget:?package=Cake.VsCode&version=0.11.1

using _Path = System.IO.Path;

string version;
string runtime = Argument<string>("arch", "osx-arm64");
string target = Argument<string>("target", "vsix");
string configuration = Argument<string>("configuration", "debug");

public string RootDirectory => MakeAbsolute(Directory(".")).ToString();
public string ArtifactsDirectory => _Path.Combine(RootDirectory, "artifacts");
public string ExtensionStagingDirectory => _Path.Combine(RootDirectory, "extension");
public string ServerProjectFilePath => _Path.Combine(RootDirectory, "src", "server", "DotRush.csproj");


Setup(context => {
	var major = DateTime.Now.ToString("yy");
	var minor = DateTime.Now.Month < 7 ? "1" : "2";
	var build = DateTime.Now.DayOfYear;
	version = $"{major}.{minor}.{build:000}";
	EnsureDirectoryExists(ArtifactsDirectory);
});

Task("clean")
	.Does(() => CleanDirectory(ExtensionStagingDirectory))
	.Does(() => CleanDirectories(_Path.Combine(RootDirectory, "src", "**", "bin")))
	.Does(() => CleanDirectories(_Path.Combine(RootDirectory, "src", "**", "obj")));

Task("server").Does(() => DotNetBuild(ServerProjectFilePath, new DotNetBuildSettings {
	Runtime = runtime,
	Configuration = configuration,
	MSBuildSettings = new DotNetMSBuildSettings {
		AssemblyVersion = version,
	},
}));


Task("vsix")
	.IsDependentOn("clean")
	.IsDependentOn("server")
	.DoesForEach<FilePath>(GetFiles("*.json"), file => {
		var regex = @"^\s\s(""version"":\s+)("".+"")(,)";
		var options = System.Text.RegularExpressions.RegexOptions.Multiline;
		ReplaceRegexInFiles(file.ToString(), regex, $"  $1\"{version}\"$3", options);
	})
	.Does(() => VscePackage(new VscePackageSettings {
		OutputFilePath = _Path.Combine(ArtifactsDirectory, $"DotRush.v{version}_{runtime}.vsix"),
		WorkingDirectory = RootDirectory,
	}));


RunTarget(target);