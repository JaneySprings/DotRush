# DotRush for NeoVim

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