using System.IO.Compression;
using System.Runtime.InteropServices;
using _Path = System.IO.Path;

public string RootDirectory => MakeAbsolute(Directory("./")).ToString();
public string ArtifactsDirectory => _Path.Combine(RootDirectory, "artifacts");
public string VSCodeExtensionDirectory => _Path.Combine(RootDirectory, "extension");

var target = Argument("target", "vsix");
var version = Argument("release-version", "1.0.0");
var configuration = Argument("configuration", "debug");

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
	}));

Task("debugging")
	.Does(() => DotNetPublish(_Path.Combine(RootDirectory, "src", "DotRush.Debugging.Host", "DotRush.Debugging.Host.csproj"), new DotNetPublishSettings {
		MSBuildSettings = new DotNetMSBuildSettings { AssemblyVersion = version },
		Configuration = configuration,
	}))
	.Does(() => DotNetPublish(_Path.Combine(RootDirectory, "src", "DotRush.Debugging.Mono", "DotRush.Debugging.Mono.csproj"), new DotNetPublishSettings {
		MSBuildSettings = new DotNetMSBuildSettings { AssemblyVersion = version },
		Configuration = configuration,
	}));

Task("diagnostics")
	.Does(() => DotNetPublish(_Path.Combine(RootDirectory, "src", "DotRush.Debugging.Diagnostics", "src", "Tools", "dotnet-trace", "dotnet-trace.csproj"), new DotNetPublishSettings {
		MSBuildSettings = new DotNetMSBuildSettings { ArgumentCustomization = args => args.Append("-p:SatelliteResourceLanguages=en") },
		OutputDirectory = _Path.Combine(VSCodeExtensionDirectory, "bin", "Diagnostics"),
		Configuration = configuration,
	})).Does(() => DotNetPublish(_Path.Combine(RootDirectory, "src", "DotRush.Debugging.Diagnostics", "src", "Tools", "dotnet-gcdump", "dotnet-gcdump.csproj"), new DotNetPublishSettings {
		MSBuildSettings = new DotNetMSBuildSettings { ArgumentCustomization = args => args.Append("-p:SatelliteResourceLanguages=en") },
		OutputDirectory = _Path.Combine(VSCodeExtensionDirectory, "bin", "Diagnostics"),
		Configuration = configuration,
	}));

Task("test")
	.IsDependentOn("clean")
	.IsDependentOn("debugging")
	.Does(() => DotNetTest(_Path.Combine(RootDirectory, "src", "DotRush.Roslyn.Server.Tests", "DotRush.Roslyn.Server.Tests.csproj"),
		new DotNetTestSettings {  
			Configuration = configuration,
			ResultsDirectory = ArtifactsDirectory,
			Loggers = new[] { "trx" }
		}
	))
	.Does(() => {
		var debuggerDirectory = _Path.Combine(VSCodeExtensionDirectory, "bin", "Debugger");
		EnsureDirectoryDeleted(debuggerDirectory);
		ExecuteCommand("dotnet", $"{_Path.Combine(VSCodeExtensionDirectory, "bin", "DevHost", "devhost.dll")} -clrdbg");
	});

Task("repack").DoesForEach(GetFiles(_Path.Combine(ArtifactsDirectory, "**", "*.vsix")), file => {
	var tempDirectory = _Path.Combine(ArtifactsDirectory, "repack");
	var outputFileName = "DotRush.Bundle.Server_" + _Path.GetFileNameWithoutExtension(file.FullPath).Split('_').Last() + ".zip";
	EnsureDirectoryDeleted(tempDirectory);
	Unzip(file, tempDirectory);
	System.IO.File.WriteAllText(_Path.Combine(tempDirectory, "extension", "extension", "bin", "LanguageServer", "_dotrush.config.json"), """
{
    "dotrush": {
        "roslyn": { }
    }
}
""");
	ZipFile.CreateFromDirectory(_Path.Combine(tempDirectory, "extension", "extension", "bin", "LanguageServer"), _Path.Combine(ArtifactsDirectory, outputFileName), CompressionLevel.Fastest, false);
	EnsureDirectoryDeleted(tempDirectory);
});

Task("vsix")
	.IsDependentOn("clean")
	.IsDependentOn("server")
	.IsDependentOn("debugging")
	.IsDependentOn("diagnostics")
	.Does(() => {
		var output = _Path.Combine(ArtifactsDirectory, $"DotRush.v{version}.vsix");
		ExecuteCommand("npm", "install");
		ExecuteCommand("vsce", $"package --out {output} --no-git-tag-version {version}");
	});


void ExecuteCommand(string command, string arguments) {
	if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
		arguments = $"/c \"{command} {arguments}\"";
		command = "cmd";
	}
	if (StartProcess(command, arguments) != 0)
		throw new Exception("Command exited with non-zero exit code.");
}

void EnsureDirectoryDeleted(string path) {
	if (DirectoryExists(path)) 
		DeleteDirectory(path, new DeleteDirectorySettings { Recursive = true, Force = true });
}

RunTarget(target);