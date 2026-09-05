//@ts-check

'use strict';

const fs = require('fs');
const path = require('path');

//@ts-check
/** @typedef {import('webpack').Configuration} WebpackConfig **/

class CopySpeedscopePlugin {
  /** @param {import('webpack').Compiler} compiler */
  apply(compiler) {
    compiler.hooks.afterEmit.tap('CopySpeedscopePlugin', () => {
      const source = path.resolve(__dirname, 'node_modules', 'speedscope');
      const target = path.resolve(__dirname, 'extension', 'speedscope');
      fs.rmSync(target, { recursive: true, force: true });
      fs.cpSync(path.join(source, 'dist', 'release'), target, { recursive: true });
      fs.copyFileSync(path.join(source, 'LICENSE'), path.join(target, 'LICENSE'));
    });
  }
}

/** @type WebpackConfig */
const extensionConfig = {
  target: 'node',
	mode: 'none',

  entry: './src/VSCode/main.ts',
  output: {
    path: path.resolve(__dirname, 'extension'),
    filename: 'main.js',
    libraryTarget: 'commonjs2'
  },
  externals: {
    vscode: 'commonjs vscode' 
  },
  resolve: {
    extensions: ['.ts', '.js']
  },
  module: {
    rules: [
      {
        test: /\.ts$/,
        exclude: /node_modules/,
        use: [
          {
            loader: 'ts-loader'
          }
        ]
      }
    ]
  },
  plugins: [ new CopySpeedscopePlugin() ],
  devtool: false,
  infrastructureLogging: {
    level: "log",
  },
};
module.exports = [ extensionConfig ];
