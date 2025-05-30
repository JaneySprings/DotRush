# DotRush for Zed

It provides all the features of the DotRush language server (including the code decompilation).

## Installation
1. Install `Rust` via [rustup](https://www.rust-lang.org/tools/install).
2. Download this folder (for example, via [this link](https://download-directory.github.io/?url=https%3A%2F%2Fgithub.com%2FJaneySprings%2FDotRush%2Ftree%2Fmain%2Fsrc%2FZed)) and place it in any location on your computer.
3. Open Zed and go to the `Extensions` tab.
4. Click on the `Install Dev Extension` button.
5. Select the folder you downloaded in step 2.
6. Restart Zed.

*If you see the `failed to spawn command` error message, try to execute the following command in the terminal:*
```bash
#MacOS
chmod +x "/Users/You/Library/Application Support/Zed/extensions/work/dotrush/LanguageServer/dotrush"

#Linux
chmod +x "/home/You/.local/share/zed/extensions/work/dotrush/LanguageServer/dotrush"
```
 
![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image5.jpg)

## Configuration

DotRush extension can be configured in Zed by creating a [settings.json file](https://zed.dev/docs/configuring-zed#settings-files) in your project root directory. 

You only need to provide the `projectOrSolutionFiles` option at minimum, but can customize the behavior further with additional settings as needed.
```json
{
    "lsp": {
        "dotrush": {
            "settings": {
                "dotrush": {
                    "roslyn": {
                        // Paths to project or solution files to load
                        "projectOrSolutionFiles": [
                            "/path/to/your/solution.sln"
                        ]
                    }
                }
            }
        }
    }
}
```

## All Configuration Options

All available configuration options can be found in the DotRush extension's [package.json](https://github.com/JaneySprings/DotRush/blob/main/package.json) file. Any option under the `dotrush.roslyn` namespace can be used in your settings file with the structure shown above.
