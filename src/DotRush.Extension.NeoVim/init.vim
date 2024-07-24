:set completeopt-=preview
:set completeopt+=menuone,noinsert

call plug#begin()
Plug 'https://github.com/neovim/nvim-lspconfig';
Plug 'https://github.com/j-hui/fidget.nvim';  "Optional extension for displaying LSP messages
call plug#end()

lua << EOF

vim.bo.omnifunc = 'vim.lsp.omnifunc'

require('lspconfig.configs').dotrush = {
    default_config = {
        cmd = { "/home/user/.lsp/dotrush/DotRush" }, -- Adjust the path to the DotRush executable
        filetypes = { 'cs', 'xaml' },
        root_dir = function(fname)
            return vim.fn.getcwd()
        end
    };
}
require('lspconfig').dotrush.setup({})