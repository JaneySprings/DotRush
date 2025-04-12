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

*This extension based on the [csharp](https://github.com/zed-extensions/csharp) extension configs.*