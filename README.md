<img align="right" width="60%" src="assets/preview.png" style="padding: 2% 0% 0% 2%"/>

### C# Development Environment for Visual Studio Code
&emsp;DotRush is a powerful, lightweight, and efficient **C# Development Environment** designed for **VS Code**. Built with performance and simplicity in mind, DotRush provides a seamless development experience for C# developers.

<br clear="right"/>

## Overview

- **C# IntelliSense** </br>
Roslyn-based autocompletion, suggestions, and code navigation to help you write code faster.

- **.NET Core Debugger** </br>
Debug your C# applications with the built-in .NET Core Debugger.

- **Unity Debugger** </br>
Debug your Unity projects with the integrated Mono Debugger.

- **Test Explorer** </br>
Run and debug your unit tests with the integrated Test Explorer.

- **Code Decompilation** </br>
Instantly decompile code with [ICSharpCode Decompiler](https://github.com/icsharpcode/ILSpy/) to view the underlying source.

- **Multitarget Diagnostics** </br>
Real-time linting and error detection to catch issues early in all target frameworks of your project.

- **Multi-platform Support** </br>
Seamless integration with both VS Code and NeoVim on Windows, macOS, and Linux.

- **Performance** </br>
Lightweight and efficient, DotRush is designed to be fast and responsive.


## Working with Projects and Solutions
&emsp;If your folder contains multiple projects or a solution file, DotRush will show the following picker for all projects and solutions in the folder. DotRush automatically saves selected projects and solutions in the workspace settings. You can open it manually by executing the `DotRush: Pick Project or Solution files` command:

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image1.jpg)


## Running and Debugging .NET Core Applications
&emsp;To run and debug your .NET Core applications, you can use the built-in .NET Core Debugger. You can start debugging by pressing **F5** and select the **.NET Core Debugger** configuration. You can also create a `launch.json` file with the following content:

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": ".NET Core Debugger (launch)",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "dotrush: Build"
        },
        {
            "name": ".NET Core Debugger (attach)",
            "type": "coreclr",
            "request": "attach",
            "processId": "${command:dotrush.pickProcess}"
        }
    ]
}
```
&emsp;You can change the startup project by clicking on it and executing the `Set as Startup Project` command from the context menu. You can also change the debugger options in the VSCode settings.

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image2.jpg)


## Running and Debugging NUnit / xUnit Tests
&emsp;To run and debug your **NUnit** or **xUnit** tests, you can use the integrated Test Explorer in VSCode. Run test by clicking on the run button next to the test or debug it by right-clicking on the run button and selecting the `Debug Test` option in the context menu.

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image3.jpg)


## Debugging Unity Projects
&emsp;To debug your Unity project, you can use the integrated Mono Debugger. Open the Unity project in VSCode (for example, by opening it from the Unity Editor) and start debugging by pressing **F5** and select the **Unity Debugger** configuration. You can also create a `launch.json` file with the following content:

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Unity Debugger",
            "type": "unity",
            "request": "attach"
        }
    ]
}
```

&emsp;You can change the debugger options in the VSCode settings.

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image4.jpg)