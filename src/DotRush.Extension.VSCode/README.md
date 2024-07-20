## Overview

<img align="right" width="64%" src="https://github.com/JaneySprings/DotRush/raw/main/assets/image1.png" style="padding: 2% 0% 0% 2%"/>

### C# Development Environment for Visual Studio Code
&emsp;DotRush is a VSCode extension for `.NET` `C#` developers designed to unlock various IDE capabilities. DotRush helps you organize multiple projects and includes features for `code editing`, `refactoring` and `error detection`. Even in the most extensive projects, DotRush is a resource-friendly companion, ensuring you stay productive and focused.

<br clear="right"/>

## Features

### Navigation & Refactoring
&emsp;DotRush simplifies code navigation and refactoring with these quick actions:

- **Go To Definition**</br>
Find all the symbol definitions in your project.

- **Go To Type Definition**</br>
Find all the symbol type definitions in your project.

- **Go To Implementation**</br>
Find all implementations of your virtual method or interface.

- **Go To References**</br>
Find all usages of the symbol in your project.

- **Renaming**</br>
Rename a class or variable by placing a cursor on it and pressing `F2` (by default).

- **Code Formatting**</br>
You can format the entire document or selected code blocs.

### DotRush vs C# Dev Kit
 
&emsp;DotRush works indepdendanly of C# Dev Kit and may conflict in certain usage scenarios. That's why you need to disable C# Dev Kit to use DotRush. Here are a few advantigures offered by DotRush:
- Efficient Resource Utilization. Experience significantly reduced resource consumption, with minimal CPU and memory usage even in large projects.
- Enhanced Multi-Targeting Support. See the following sections for additional information: IntelliSense in Multi-Target Projects, Cross-Platform Error Detection.
- Integration with .NET Meteor features. For example, .NET Meteor may pass additional information to the Watch window enhancing debugging capabilities.
 
&emsp;It's essential to note some limitations when compared to the C# Dev Kit:
- Blazor projects are not supported.
- Built-in .NET Core debugger is not supporeted, but I'm working on it.

### IntelliSense in Multi-Target Projects
&emsp;DotRush offers IntelliSense for multiple Target Frameworks. For example, when you're working on a cross-platform application and your current target framework is Android, DotRush provides IntelliSense capabilities for iOS, too.

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image2.png)


### Cross-Platform Error Detection
&emsp;DotRush highlights errors and displays descriptions in all Target Frameworks used in your project.

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image3.png)


### Multiple Project Management
&emsp;Empowering you with [Code Workspaces](https://code.visualstudio.com/docs/editor/workspaces) DotRush lets you effortlessly combine multiple projects and apply different settings to them. DotRush seamlessly activates all features when you add a new folder to your workspace. **Note that `.sln` files are not supported.**

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image4.png)


### Quick Fixes
&emsp;Activate error quick fixes by hovering over an error or clicking on the lamp icon:

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image5.png)


### Code Decompiling
&emsp;When navigating to a class not included in your project, DotRush will decompile it with [ICSharpCode Decompiler](https://github.com/icsharpcode/ILSpy/), allowing you to explore external libraries.

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image6.png)

### Code Quality Analysis
&emsp;Integrate code quality analyzers into your project, and DotRush will highlight suggestions from these analyzers. This is disabled by default, but you can enable the `Enable Roslyn Analyzers` option in the settings of the extension.

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image7.png)


## Prerequisites
- **Framework:** `.NET 6/7/8`
- **Operating System:** `Windows`, `Linux`, `MacOS`
- **CPU Architecture:** `x64`, `ARM64`


## More for VSCode Developers
&emsp;Check out [.NET Meteor](https://github.com/JaneySprings/DotNet.Meteor) – a cross-platform VSCode extension to build, debug .NET apps and deploy them to devices or emulators.


## About the Author
&emsp;I'm Nikita Romanov, a passionate programming enthusiast with a focus on .NET MAUI. I work with an amazing team at `DevExpress` to make the lives of developers around us easier. Our team is dedicated to creating a comprehensive [mobile component suite](https://www.devexpress.com/maui) for .NET MAUI which is currently available `free-of-charge`. In my free time, I work on my hobby projects, `DotNet.Meteor` and `DotRush`, which are always open to feedback and contributions. Feel free to share your thoughts with me, and **let's make the .NET community even better together!**
