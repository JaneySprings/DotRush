<img align="right" width="60%" src="https://github.com/JaneySprings/DotRush/raw/main/assets/preview.png" style="padding: 2% 0% 0% 2%"/>

### C# Development Environment for Visual Studio Code and NeoVim
&emsp;DotRush is a powerful, lightweight, and efficient **C# Language Server Extension** designed for **VS Code** and **NeoVim**. Built with performance and simplicity in mind, DotRush empowers developers with robust C# development tools in their favorite editors.

<br clear="right"/>


## Features

- **C# IntelliSense** </br>
Advanced autocompletion and suggestions to boost your productivity.

- **Code Navigation** </br>
Effortlessly navigate to definitions, references, and symbols.

- **Code Decompilation** </br>
Instantly decompile code with [ICSharpCode Decompiler](https://github.com/icsharpcode/ILSpy/) to view the underlying source.

- **Multitarget Diagnostics** </br>
Real-time linting and error detection to catch issues early in all target frameworks of your project.

- **Formatting** </br>
Automatic code formatting for clean, consistent code.

- **Multi-platform Support** </br>
Seamless integration with both VS Code and NeoVim on Windows, macOS, and Linux.

- **Performance** </br>
Lightweight and efficient, DotRush is designed to be fast and responsive.

- **Additional Features for VSCode** </br>
DotRush provides additional features for Visual Studio Code users, such as `.NET Core Debugger` and `Test Explorer`.


## Installation

**For VS Code**
1. Open the Extensions view.
2. Search for [DotRush](https://marketplace.visualstudio.com/items?itemName=nromanov.dotrush).
3. Click Install.

**For NeoVim**
1. Ensure you have a plugin manager like [vimplug](https://github.com/junegunn/vim-plug) installed.
2. Download latest DotRush executable from the GitHub `releases`.
3. Add the following to your init.vim configuration:
```lua
call plug#begin()
Plug 'https://github.com/neovim/nvim-lspconfig'
call plug#end()

lua << EOF

require('lspconfig.configs').dotrush = {
    default_config = {
        cmd = { "C:\\Users\\Test\\.lsp\\dotrush\\DotRush.exe" }, -- Adjust the path to the DotRush executable
        filetypes = { 'cs', 'xaml' },
        root_dir = function(fname)
            return vim.fn.getcwd()
        end
    };
}
require('lspconfig').dotrush.setup({})
```

## Links

- For additional information for **VSCode**, check out the [README](https://github.com/JaneySprings/DotRush/blob/main/src/VSCode/README.md).

- For additional information for **NeoVim**, check out the [README](https://github.com/JaneySprings/DotRush/blob/main/src/NeoVim/Readme.md).