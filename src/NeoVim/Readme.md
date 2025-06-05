# DotRush for Neovim

It provides all the features of the DotRush language server (including the code decompilation).

## Installation

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

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image6.jpg)

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