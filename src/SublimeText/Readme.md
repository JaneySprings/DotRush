# DotRush for Sublime Text

It provides all the features of the DotRush language server (including the code decompilation).

## Installation
1. Install `LSP` plugin for Sublime Text (**Package Control: Install Package** -> **LSP**).
2. Download the latest release of the DotRush server from [GitHub Releases](https://github.com/JaneySprings/DotRush/releases).
2. Open `LSP` settings file (**Preferences** -> **Package Settings** -> **LSP** -> **Settings**).
3. Create the following configuration in the `LSP` settings file:
```json
{
	"clients": {
        "dotrush": {
            "enabled": true,
            "command": ["Your\\Path\\To\\DotRush.exe"],
            "selector": "source.cs",
        }
    }
}
```

## Configuration

DotRush extension can be configured by creating a `dotrush.config.json` file in your project root directory. 

You only need to provide the `projectOrSolutionFiles` option at minimum, but can customize the behavior further with additional settings as needed.

```json
{
    "dotrush": {
        "roslyn": {
            // Paths to project or solution files to load
            "projectOrSolutionFiles": [
                "/path/to/your/solution.sln"
            ]
        }
    }
}
```

### All Configuration Options

All available configuration options can be found in the DotRush extension's [package.json](https://github.com/JaneySprings/DotRush/blob/main/package.json) file. Any option under the `dotrush.roslyn` namespace can be used in your settings file with the structure shown above.