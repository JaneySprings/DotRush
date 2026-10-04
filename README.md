[![CodeQL Advanced](https://github.com/JaneySprings/DotRush/actions/workflows/codeql.yml/badge.svg)](https://github.com/JaneySprings/DotRush/actions/workflows/codeql.yml)

## Overview

- **C# IntelliSense** </br>
Roslyn-based autocompletion, suggestions, and code navigation.

- **.NET Core Debugger** </br>
Debug your C# applications with the built-in .NET Core Debugger.

- **.NET Core Profiler** </br>
Profile your .NET Core applications with the built-in profiler tools.

- **Unity Debugger** </br>
Debug your Unity projects with the integrated Mono Debugger.

- **Test Explorer** </br>
Run and debug your unit tests with the integrated Test Explorer.

- **Code Decompilation** </br>
Instantly decompile code with [ICSharpCode Decompiler](https://github.com/icsharpcode/ILSpy/) to view the underlying source.

- **Multitarget Diagnostics** </br>
Real-time linting and error detection to catch issues early in all target frameworks of your project.

## Working with Projects and Solutions
&emsp;If your folder contains multiple projects or a solution file, DotRush will show the following picker for all projects and solutions in the folder. DotRush automatically saves selected projects and solutions in the workspace settings. You can open it manually by executing the `DotRush: Pick Project or Solution files` command:

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image1.jpg)


## Running and Debugging .NET Core Applications
&emsp;To run and debug your .NET Core applications, you can use the built-in .NET Core Debugger. You can start debugging by pressing **F5** and select the **.NET Core Debugger** configuration. You can also create a `launch.json` file with the following content:

```jsonc
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": ".NET Core Debugger (launch)",
            "type": "coreclr",
            "request": "launch",
            "program": "${command:dotrush.activeTargetPath}",
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


## Profiling .NET Core Applications
&emsp;DotRush provides three profiling tools for your .NET Core applications: the realtime **Performance View**, the **Trace Profiler** and the **Memory Dump**. The Performance View starts automatically together with the **.NET Core Debugger**. The Trace Profiler and the Memory Dump can be attached to any running process by executing the `DotRush: Attach Trace Profiler` or `DotRush: Create Heap Dump` commands. Also you can use the extra buttons, located in the debugger toolbar, if you have the **.NET Core Debugger** running.

### Performance View
&emsp;When the **.NET Core Debugger** is connected to your application, the **Performance** view appears in the `Run and Debug` side bar. It uses the built-in **dotnet-counters** tool and shows two realtime charts of the debugged process: the **CPU** usage with the **Time in GC**, and the **Working Set** with the **GC Heap** size:

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image5.png)

### Trace Profiler
&emsp;To find out where your application spends its time and what it allocates, execute the `DotRush: Attach Trace Profiler` command. It runs the built-in **dotnet-trace** tool in the terminal. Press **Enter** in the terminal to stop the collection and open the generated `*.nettrace.json` file from the explorer. It is displayed in the built-in [speedscope](https://www.speedscope.app) viewer. Use the profile selector at the top to switch between the profiles:

- **CPU (all threads)** shows where the managed code of the whole application spends CPU time.
- **Allocations** is weighted in bytes and shows the allocated types with the call stacks that allocate them (a sampled estimate, about one sample per 100 KB).
- **Thread** profiles show the wall clock timeline of every thread including the time it is blocked.

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image6.png)

### Memory Dump
&emsp;To inspect the managed heap of your application, execute the `DotRush: Create Heap Dump` command. It runs the built-in **dotnet-gcdump** tool and takes a snapshot of all live objects. Open the generated `*.gcdump.json` file from the explorer. It is displayed in the built-in _memory viewer_. Use the tabs at the top to switch between the views:

- **Summary** shows the heap types with their instance count, shallow size and retained size. Expand a type to see its instances.
- **Dominators** shows the dominator tree: each object is placed under the single object that keeps it alive, biggest retained size first.
- **GC Roots** shows the static fields, thread stacks and handles, followed along their references.
- **Graph** shows the retention graph of the selected object: its retainers toward the GC roots above and its references below.
- **Leak Suspects** shows the detected leak patterns, most severe first (objects kept alive only by delegates, event handlers with many subscribers, static fields holding the most memory and others).
- **Compare** shows the difference of the types against a second snapshot of the same process.

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image7.png)


## Running and Debugging NUnit / xUnit / MSTest Tests
&emsp;To run and debug your **VSTest** tests, you can use the integrated Test Explorer in VSCode. Run test by clicking on the run button next to the test or debug it by right-clicking on the run button and selecting the `Debug Test` option in the context menu.

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image3.jpg)


## Debugging Unity Projects
&emsp;To debug your Unity project, you can use the integrated Mono Debugger. Open the Unity project in VSCode (for example, by opening it from the Unity Editor) and start debugging by pressing **F5** and select the **Unity Debugger** configuration. You can also create a `launch.json` file with the following content:

```jsonc
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Unity Debugger",
            "type": "unity",
            // Attach to Android device
            // "transportArgs": {
            //     "type": "android"
            // },
            "request": "attach"
        }
    ]
}
```

&emsp;You can change the debugger options in the VSCode settings.

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image4.jpg)


## Debugging Godot Projects
&emsp;To debug your Godot project, open it in VSCode and create a `launch.json` file with the following content (adjust the `program` to the location of your Godot Engine executable):
```jsonc
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": ".NET Core Debugger (launch)",
            "type": "coreclr",
            "request": "launch",
            "program": "C:\\Programs\\Godot\\Godot_v4.4.1-stable_mono_win64.exe",
            "preLaunchTask": "dotrush: Build"
        }
    ]
}
```

&emsp;Press **F5** to start debugging. It will launch the Godot Engine and attach the debugger to it:

![image](https://github.com/JaneySprings/DotRush/raw/main/assets/image8.jpg)


## Limitations
&emsp;DotRush currently supports **only C# language** features and does not support `Razor`, `XAML`, or other languages. Also it does not support the following language features:

- **CodeLens** </br>
DotRush does not support [CodeLens features](https://code.visualstudio.com/api/language-extensions/programmatic-language-features#codelens-show-actionable-context-information-within-source-code) such as references, tests, and other CodeLens features.

## Alternative Editors
You can also use DotRush with other editors. See the [following repository](https://github.com/JaneySprings/dotrush-alt-editors) files for more information.