using System.IO.Compression;
using System.Runtime.InteropServices;
using _Path = System.IO.Path;

public string RootDirectory => MakeAbsolute(Directory("./")).ToString();
public string ArtifactsDirectory => _Path.Combine(RootDirectory, "artifacts");
public string VSCodeExtensionDirectory => _Path.Combine(RootDirectory, "extension");

var target = Argument("target", "vsix");
var version = Argument("release-version", "1.0.0");
var configuration = Argument("configuration", "debug");
var runtime = Argument("arch", RuntimeInformation.RuntimeIdentifier);
var bundle = HasArgument("bundle");

Setup(context => {
	var date = DateTime.Now;
	version = $"{DateTime.Now.ToString("yy")}.{date.ToString("%M")}.{date.DayOfYear}";
	EnsureDirectoryExists(ArtifactsDirectory);
});

Task("clean")
	.Does(() => {
		EnsureDirectoryExists(ArtifactsDirectory);
		CleanDirectories(_Path.Combine(RootDirectory, "src", "DotRush.*", "**", "bin"));
		CleanDirectories(_Path.Combine(RootDirectory, "src", "DotRush.*", "**", "obj"));
		CleanDirectories(VSCodeExtensionDirectory);
	});

Task("server")
	.Does(() => DotNetPublish(_Path.Combine(RootDirectory, "src", "DotRush.Roslyn.Server", "DotRush.Roslyn.Server.csproj"), new DotNetPublishSettings {
		MSBuildSettings = new DotNetMSBuildSettings { AssemblyVersion = version },
		Configuration = configuration,
		Runtime = runtime,
	}))
	.Does(() => {
		var input = _Path.Combine(VSCodeExtensionDirectory, "bin", "LanguageServer");
		var output = _Path.Combine(ArtifactsDirectory, $"DotRush.Bundle.Server_{runtime}.zip");
		EnsureFileDeleted(output);
		ZipFile.CreateFromDirectory(input, output, CompressionLevel.Optimal, false);
	});

Task("debugging")
	.Does(() => DotNetPublish(_Path.Combine(RootDirectory, "src", "DotRush.Debugging.NetCore", "DotRush.Debugging.NetCore.csproj"), new DotNetPublishSettings {
		MSBuildSettings = new DotNetMSBuildSettings { AssemblyVersion = version },
		Configuration = configuration,
		Runtime = runtime,
	}))
	.Does(() => DotNetPublish(_Path.Combine(RootDirectory, "src", "DotRush.Debugging.Mono", "DotRush.Debugging.Mono.csproj"), new DotNetPublishSettings {
		MSBuildSettings = new DotNetMSBuildSettings { AssemblyVersion = version },
		Configuration = configuration,
		Runtime = runtime,
	}))
	.Does(() => {
		if (!bundle) return;
		ExecuteCommand("dotnet", $"{_Path.Combine(VSCodeExtensionDirectory, "bin", "TestExplorer", "dotrushde.dll")} --install-ncdbg");
	});

Task("diagnostics")
	.Does(() => DotNetPublish(_Path.Combine(RootDirectory, "src", "DotRush.Debugging.Diagnostics", "src", "Tools", "dotnet-trace", "dotnet-trace.csproj"), new DotNetPublishSettings {
		OutputDirectory = _Path.Combine(VSCodeExtensionDirectory, "bin", "Diagnostics"),
		Configuration = configuration,
		Runtime = runtime,
	})).Does(() => DotNetPublish(_Path.Combine(RootDirectory, "src", "DotRush.Debugging.Diagnostics", "src", "Tools", "dotnet-gcdump", "dotnet-gcdump.csproj"), new DotNetPublishSettings {
		OutputDirectory = _Path.Combine(VSCodeExtensionDirectory, "bin", "Diagnostics"),
		Configuration = configuration,
		Runtime = runtime,
	}));

Task("test")
	.IsDependentOn("clean")
	.Does(() => DotNetTest(_Path.Combine(RootDirectory, "src", "DotRush.Roslyn.Workspaces.Tests", "DotRush.Roslyn.Workspaces.Tests.csproj"),
		new DotNetTestSettings {  
			Configuration = configuration,
			Verbosity = DotNetVerbosity.Quiet,
			ResultsDirectory = ArtifactsDirectory,
			Loggers = new[] { "trx" }
		}
	))
	.Does(() => DotNetTest(_Path.Combine(RootDirectory, "src", "DotRush.Roslyn.CodeAnalysis.Tests", "DotRush.Roslyn.CodeAnalysis.Tests.csproj"),
		new DotNetTestSettings {  
			Configuration = configuration,
			Verbosity = DotNetVerbosity.Quiet,
			ResultsDirectory = ArtifactsDirectory,
			Loggers = new[] { "trx" }
		}
	))
	.Does(() => DotNetTest(_Path.Combine(RootDirectory, "src", "DotRush.Debugging.NetCore.Tests", "DotRush.Debugging.NetCore.Tests.csproj"),
		new DotNetTestSettings {  
			Configuration = configuration,
			Verbosity = DotNetVerbosity.Quiet,
			ResultsDirectory = ArtifactsDirectory,
			Loggers = new[] { "trx" }
		}
	));

Task("vsix")
	.IsDependentOn("clean")
	.IsDependentOn("server")
	.IsDependentOn("debugging")
	.IsDependentOn("diagnostics")
	.Does(() => {
		var vsruntime = runtime.Replace("win-", "win32-").Replace("osx-", "darwin-");
		var suffix = bundle ? ".Bundle" : string.Empty;
		var output = _Path.Combine(ArtifactsDirectory, $"DotRush{suffix}.v{version}_{vsruntime}.vsix");
		ExecuteCommand("npm", "install");
		ExecuteCommand("vsce", $"package --target {vsruntime} --out {output} --no-git-tag-version {version}");
	});


void ExecuteCommand(string command, string arguments) {
	if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
		arguments = $"/c \"{command} {arguments}\"";
		command = "cmd";
	}
	if (StartProcess(command, arguments) != 0)
		throw new Exception("Command exited with non-zero exit code.");
}
void EnsureFileDeleted(string path) {
	if (FileExists(path)) 
		DeleteFile(path);
}

RunTarget(target);