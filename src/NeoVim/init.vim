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