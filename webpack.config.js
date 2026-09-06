//@ts-check

'use strict';

const fs = require('fs');
const path = require('path');

//@ts-check
/** @typedef {import('webpack').Configuration} WebpackConfig **/

/** Copies the release build of a web app (speedscope, memoryviewer) into the extension folder so webviews can load it. */
class CopyWebAppPlugin {
  /**
   * @param {string} name folder name under extension/
   * @param {string[]} sources candidate source directories, the first existing one is used
   * @param {string} [license] license file to copy next to the release files
   */
  constructor(name, sources, license) {
    this.name = name;
    this.sources = sources;
    this.license = license;
  }
  /** @param {import('webpack').Compiler} compiler */
  apply(compiler) {
    compiler.hooks.afterEmit.tap('CopyWebAppPlugin', () => {
      const source = this.sources.find(candidate => fs.existsSync(candidate));
      const target = path.resolve(__dirname, 'extension', this.name);
      if (source === undefined) {
        if (!fs.existsSync(target))
          console.warn(`[CopyWebAppPlugin] no release build found for '${this.name}', looked in: ${this.sources.join(', ')}`);
        return;
      }
      fs.rmSync(target, { recursive: true, force: true });
      fs.cpSync(source, target, { recursive: true });
      if (this.license !== undefined && fs.existsSync(this.license))
        fs.copyFileSync(this.license, path.join(target, 'LICENSE'));
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
  plugins: [
    new CopyWebAppPlugin('speedscope',
      [path.resolve(__dirname, 'node_modules', 'speedscope', 'dist', 'release')],
      path.resolve(__dirname, 'node_modules', 'speedscope', 'LICENSE')),
    // The npm package is preferred; the sibling checkout is used while developing the viewer locally
    new CopyWebAppPlugin('memoryview', [
      path.resolve(__dirname, 'node_modules', 'memoryviewer', 'dist', 'release'),
      path.resolve(__dirname, '..', 'MemoryViewer', 'dist', 'release'),
    ]),
  ],
  devtool: false,
  infrastructureLogging: {
    level: "log",
  },
};
module.exports = [ extensionConfig ];
