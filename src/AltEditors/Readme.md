## Important Note
**This features is not actively maintained and may not work as expected. It was primarily created to support the DotRush extension for Visual Studio Code. If you encounter issues or need assistance, please consider using the DotRush extension in VSCode for the best experience.**

## DotRush with Neovim
1. Ensure you have a plugin manager like [vimplug](https://github.com/junegunn/vim-plug) installed.
2. Download the `DotRush.Server` executable from the latest [GitHub release](https://github.com/JaneySprings/DotRush/releases/latest) and place it in any location on your computer.
3. Add the following lines to your init.vim configuration:

```lua
call plug#begin()
Plug 'https://github.com/neovim/nvim-lspconfig'
call plug#end()

lua << EOF
require('lspconfig.configs').dotrush = {
    default_config = {
        cmd = { '/Path/To/Executale/dotrush' }, -- Adjust path to the DotRush executable
        filetypes = { 'cs', 'xaml' },
        root_dir = function(fname)
            return vim.fn.getcwd()
        end
    };
}
require('lspconfig').dotrush.setup({})
EOF
```

## DotRush with Zed
1. Install `Rust` via [rustup](https://www.rust-lang.org/tools/install).
2. Download the `Zed` folder and place it in any location on your computer.
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

## DotRush with Sublime Text
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
DotRush can be configured by creating a `dotrush.config.json` file in your project root directory or next to the server executable.

You only need to provide the `projectOrSolutionFiles` option if the server can't detect a project to load automaticaly. You can customize the behavior with additional settings as needed.
```json
{
    "dotrush": {
        "roslyn": {
            "projectOrSolutionFiles": [
                "/path/to/your/solution.sln"
            ]
        }
    }
}
```

All available configuration options can be found in the DotRush extension's [package.json](https://github.com/JaneySprings/DotRush/blob/main/package.json) file. Any option under the `dotrush.roslyn` namespace can be used in your settings file with the structure shown above.