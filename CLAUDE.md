# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Layout

DotRush 是一个 VS Code C# 开发扩展。仓库同时包含 .NET 后端进程（LSP / Debugger / Host）与 TypeScript 编写的 VS Code 扩展前端，两者通过 LSP/JSON-RPC/CLI 通信。

- `src/VSCode/` — TypeScript 编写的 VS Code 扩展前端（`main.ts` 为激活入口）。`webpack` 打包到仓库根的 `extension/main.js`。
- `src/DotRush.Roslyn.Server/` — Roslyn 驱动的 LSP 服务器（输出可执行文件名 `DotRush`）。
- `src/DotRush.Roslyn.Workspaces/` — MSBuild 工作区加载、文件监听、解决方案/项目控制器抽象。
- `src/DotRush.Roslyn.CodeAnalysis/` — 编译诊断、CodeAction/CodeRefactoring 宿主、分析器加载缓存。
- `src/DotRush.Roslyn.Navigation/` — 导航（定义/引用/层级）以及通过 `ICSharpCode.Decompiler` 的元数据反编译。
- `src/DotRush.Roslyn.ExternalAccess/` — 通过命名管道的 `StreamJsonRpc` 外部 RPC 接口（供其他工具访问当前工作区）。
- `src/DotRush.Roslyn.Server.Tests/` — 唯一的测试项目（NUnit），通过 `BaseProjectTestFixture` 在 sandbox 中创建真实 csproj/解决方案。
- `src/DotRush.Common/` — 共享工具：日志、扩展方法、MSBuild 评估、`DefaultItemsRewriter`、运行时信息。
- `src/DotRush.Debugging.Host/` — 名为 `devhost` 的多用途 CLI：项目评估（`-p`）、进程列表（`-ps`）、模板引擎（`new`）、VSTest 测试宿主（`test`）、`vsdbg`/`ncdbg` 调试器下载安装。
- `src/DotRush.Debugging.Mono/` — Mono Soft Debugger 适配器（输出 `monodbg`，用于 Unity）。
- `src/AltEditors/` — Zed/Neovim/Sublime 等替代编辑器配置（非主要维护）。
- `src/DotRush.Debugging.Diagnostics/`, `src/DotRush.Debugging.MonoLib/`, `src/DotRush.LanguageServer.Framework/` — **Git submodule**，分别引入 `dotnet/diagnostics`（dotnet-trace、dotnet-gcdump）、`Mono.Debugger`、定制的 LSP Framework。**首次 clone 必须 `git submodule update --init --recursive`**。
- `build.cake` — Cake 脚本，所有构建/发布命令都从这里走。
- `Common.Build.props`、`Directory.Packages.props` — 全部 .NET 项目共享 TFM、`NoWarn`、中央包版本管理。

## Build & Run Commands

`build.cake` 通过 `dotnet cake --target=<task>` 调用，常用 target：

```bash
# 安装 Cake 工具（仅首次）
dotnet tool restore

# 完整打包 VSIX（清理→server→debugging→diagnostics→vsce package）
dotnet cake --target=vsix --configuration=release

# 仅构建 LSP 服务器到 extension/bin/LanguageServer
dotnet cake --target=server

# 构建调试主机（devhost）和 Mono 调试器，bundle 模式还会拉取 ncdbg
dotnet cake --target=debugging

# 拉取 dotnet-trace、dotnet-gcdump 到 extension/bin/Diagnostics
dotnet cake --target=diagnostics

# 跨平台运行测试，产物在 artifacts/*.trx
dotnet cake --target=test --configuration=release

# 指定 RID 构建
dotnet cake --target=vsix --arch=win-x64
dotnet cake --target=vsix --arch=linux-arm64
```

`Setup` 阶段会用 `yy.M.dayOfYear` 自动覆盖版本号，因此 `--release-version=` 参数实际只作为 fallback。

### 前端（TypeScript）

```bash
npm install                # 装依赖（vsce package 也会自动调）
npm run watch              # tsc -w，监听 src/VSCode/tsconfig.json
npm run package            # webpack production，输出 extension/main.js
```

VS Code 中按 F5 启动 `Run Extension` launch.json 配置即可在扩展开发宿主里调试前端；`.NET Attach to LSP` 用于附加到已启动的 `DotRush` LSP 进程。

### 跑单个测试

测试基于 NUnit。在 `src/DotRush.Roslyn.Server.Tests/` 下：

```bash
# 整个类
dotnet test --filter "FullyQualifiedName~MSBuildProjectLoaderTests"
# 单个方法
dotnet test --filter "Name=SpecificTestMethodName"
```

Cake 的 `test` target 会先依赖 `clean` 和 `debugging`，因为部分测试依赖已构建好的 devhost。手工 `dotnet test` 时同样需要先 `cake --target=debugging`，否则测试基类无法解析 `devhost.dll` 路径。

## High-Level Architecture

### 三个进程模型

整套扩展运行时是「VS Code 扩展（Node）」+「LSP Server（.NET）」+「按需启动的 devhost/monodbg/clrdbg 子进程」三层。前端从不直接读写解决方案，所有 Roslyn 操作都走 LSP；MSBuild 求值、模板创建、进程枚举、VSTest 运行等任何「非 LSP」工作都通过 `devhost` 子进程（`src/VSCode/interop/interop.ts:13`）以 JSON in/out 或 JSON-RPC 通信。

```
┌──────────────────────────┐        LSP (stdin/stdout)         ┌────────────────────────────┐
│ VS Code 扩展 (TypeScript)│ ◄──────────────────────────────► │ DotRush.Roslyn.Server      │
│  - controllers/*         │                                    │  - Handlers (TextDocument/ │
│  - DebugAdapter 注册     │        spawn + JSON / JSON-RPC     │     Workspace/Framework)   │
│  - TestExplorer 主控     │ ──────────┐                        │  - WorkspaceService        │
└──────────────────────────┘           │                        │  - CodeAnalysisService     │
            │                          ▼                        │  - NavigationService       │
            │ DAP                ┌──────────────┐               │  - ExternalAccess pipe     │
            ▼                    │  devhost CLI │               └────────────────────────────┘
   ┌───────────────────┐         │  (test/new/  │
   │ clrdbg / monodbg  │         │   -p/-ps)    │
   └───────────────────┘         └──────────────┘
```

### LSP 服务器（`DotRush.Roslyn.Server/Main.cs`）

- 入口禁用了 `Console.Out`/`Console.Error`，因为 stdout 用于 LSP 帧传输；调试请用 `CurrentSessionLogger`（NLog，写入文件）而不是 `Console.Write*`。
- 通过 submodule 内的 `DotRush.LanguageServer.Framework`（fork 自 `EmmyLua.LanguageServer.Framework`）实现 LSP 协议。Handler 全部在 `Main.cs` 中通过 `languageServer.AddHandler` 注册，按 `Handlers/{TextDocument,Workspace,Framework}/` 三类组织。
- 6 个核心服务通过构造函数手工注入（没有 DI 容器）：`ConfigurationService → WorkspaceService → CodeAnalysisService / NavigationService / TestExplorerService / ExternalAccessService`。
- `OnInitializeAsync` 会监听 IDE 进程 PID，对方退出立即 `Environment.Exit(0)`，避免孤儿 LSP 进程。
- 自定义协议扩展（前后端约定的非标准方法）：
  - `dotrush/projectLoaded`（server → client，单个项目加载完成时推送 `MSBuildProject` 模型）
  - `dotrush/loadCompleted`（server → client，所有工作区加载完成）
  - `dotrush/reloadWorkspace`（client → server，前端检测到 `.csproj`/`.props` 保存时触发）
  - `dotrush/solutionDiagnostics`（client → server，build 任务结束后触发整解决方案诊断扫描）
  - `dotrush/testExplorer/fixtures` / `dotrush/testExplorer/tests`（client → server，懒加载测试树）

### 工作区加载链（`DotRush.Roslyn.Workspaces`）

`DotRushWorkspace` ← `SolutionController` ← `ProjectsController`，是「工作区生命周期 = 一组虚回调」的设计：`OnProjectRestoreStarted/Completed/Failed`、`OnProjectLoadStarted/Completed`、`OnProjectCompilationStarted/Completed`、`OnLoadingStarted/CompletedAsync` 在加载流程的每一步触发，子类（`WorkspaceService`）把它们映射成 LSP 的 `WorkDoneProgress` + `dotrush/projectLoaded` 通知 + `PublishDiagnostics`（用 `NU0000` 上报 restore 失败）。新增工作区相关 UI 反馈时，应该在这里加回调点，**不要**在 controller 里直接调 LSP。

加载入口：
1. 前端通过 `LanguageServerController.shouldQuickPickTargets()` 选择 `.csproj`/`.sln`/`.slnf`/`.slnx`，写入 `dotrush.roslyn.projectOrSolutionFiles` 工作区设置（[src/VSCode/controllers/languageServerController.ts:94](src/VSCode/controllers/languageServerController.ts)）。
2. LSP 端 `WorkspaceService.GetProjectOrSolutionFiles()` 优先使用配置中显式声明的目标，否则在 workspace folder 里自动探测：单个 `.sln` 或单个 `.csproj` 才会自动加载，多个则要求用户选择。
3. MSBuild SDK 通过 `MSBuildLocator.RegisterDefaults()` 注册；若 `dotrush.roslyn.dotnetSdkDirectory` 或 `DOTNET_SDK_PATH` 环境变量提供了路径则优先用它，最后兜底用 `DotRush.Common.MSBuild.MSBuildLocator.GetLatestSdkLocation()`。
4. 加载后启动 `WorkspaceFilesWatcher`，将 `IWorkspaceChangeListener` 事件回灌到 `WorkspaceService`，支持 `ApplyWorkspaceChanges` 模式下用 `DefaultItemsRewriter` 实际改写 `.csproj`。

### 诊断流水线（`CodeAnalysisService` + `CompilationHost`）

- 单个**串行**后台 worker 线程（[CodeAnalysisService.cs:37](src/DotRush.Roslyn.Server/Services/CodeAnalysisService.cs)）；每次入队的 task 在执行前会 drain 队列，**只跑最后一个**——这是处理快速连击编辑的去抖策略。新增需要重算诊断的入口请走 `RequestDiagnosticsPublishing` 而不是直接调 `AnalyzeAsync`。
- 编译器诊断与分析器诊断各自有独立的 `AnalysisScope`（None/Document/Project/Solution），由配置 `dotrush.roslyn.compilerDiagnosticsScope` 与 `analyzerDiagnosticsScope` 控制。
- `DiagnosticsFormat`：`NoHints` 会过滤被标记为 UI 隐藏的诊断，`InfosAsHints` 把 Info 级别在客户端降级显示。

### Dispatcher（线程调度器）

`dotrush.roslyn.dispatcherType` 提供三种 LSP 请求调度策略：`SingleThread`、`MultiThread`（默认）、`PerformanceCounter`。`PerformanceCounter` 实现在 `Dispatchers/PerformanceCounterDispatcher.cs`，会把每个请求耗时打到日志，适合诊断慢请求。

### VS Code 扩展前端（`src/VSCode/`）

- 入口 `main.ts` 按需激活 6 个 controller。**当 `vscode.workspace.workspaceFolders === undefined` 时只激活 Debug/Template/Modules/Performance/ExternalTypeResolver**——LSP 不会启动，避免 single-file 模式报错。
- `LanguageServerController` 启动二进制位于 `extension/bin/LanguageServer/DotRush[.exe]`（与 cake `server` target 输出位置一致）。修改 LSP 启动参数请同步改 `serverOptions`。
- `DebugAdapterController` 注册了 `coreclr` 与 `unity` 两个 debug type；首次启动时若 `extension/bin/Debugger/` 不存在，会通过 `devhost` 子进程下载 vsdbg（VSCode 官方版）或 ncdbg（fork 版）——`Extensions.onVSCode` 根据 `vscode.env.appName.includes('Visual Studio Code')` 区分二者。
- `TestExplorerController` 与 LSP 协作：LSP 推 `dotrush/projectLoaded` 告诉前端哪些项目是测试项目；前端按需通过 `dotrush/testExplorer/fixtures` 与 `dotrush/testExplorer/tests` 懒加载测试树；运行测试时通过 `Interop.createTestHostRpc` 启动 `devhost test`，用 `vscode-jsonrpc` 全双工通信。
- `Interop.getPropertyValue` 通过 `dotnet msbuild -getProperty:` 获取 `TargetPath` 等属性——这是 `${command:dotrush.activeTargetPath}` 等命令的实现路径。

### `devhost` 子命令 (`src/DotRush.Debugging.Host/Main.cs`)

| 参数 | 用途 |
| ---- | ---- |
| `-p <project>` | 输出 `MSBuildProject` JSON，前端 `Interop.getProject` 使用 |
| `-ps` | 输出当前进程列表，供 `dotrush.pickProcess` |
| `-vsdbg` / `-ncdbg` | 下载并安装对应调试器到 `extension/bin/Debugger/` |
| `new -l` / `new -i <id> -o <dir> -p <json>` | 模板引擎 IDE 接口，列出 / 创建项目 |
| `test -a <asm> [-f filter] [-s settings] [-d]` | 启动 VSTest 宿主，通过 stdin/stdout 用 StreamJsonRpc 反向通信测试运行结果与（可选）`attachDebuggerToProcess` 请求 |

新增子命令时使用 `System.CommandLine` 在 `Main.cs` 中注册，输出格式统一为 `JsonSerializer.Serialize(...)` 的单行 JSON——前端 `ProcessRunner` 依赖这一点。

### 测试基础设施

测试不使用 mock：每个 fixture 在 `${OutDir}/Sandbox/<ProjectName>/` 真实生成 `.csproj`/`.sln` 文件并调用 `WorkspaceService` 加载。

- `BaseProjectTestFixture` 抽象类：实现 `CreateProjectFileContent()` 即可获得初始化好的 `WorkspaceService` 与沙箱目录，并由 `OneTimeSetUp`/`OneTimeTearDown` 自动清理。
- 派生类：`NUnitTestProjectFixture`、`SimpleWorkspaceFixture`、`MultitargetProjectFixture` 分别对应单 TFM 测试项目、纯 C# 项目、`<TargetFrameworks>` 多目标项目。新增场景时尽量复用这些 fixture 的派生结构。

## Conventions Worth Knowing

- **C# 目标框架由 `Common.Build.props` 集中控制**：库默认 `net8.0`（`TargetFramework`），LSP 服务器与测试用更高的 `ServerTargetFramework=net10.0`（[Common.Build.props:5](src/Common.Build.props)）。这两者都通过 `<RollForward>major</RollForward>` 允许向上漂移。
- **中央包版本管理**：`ManagePackageVersionsCentrally=true`，新增 NuGet 包要在 `src/Directory.Packages.props` 加 `<PackageVersion>`，子项目只写 `<PackageReference Include="..." />`。Roslyn 全系包统一走 `$(CodeAnalysisVersion)`。
- **不要在 LSP 写 stdout**：`Main.cs` 已经显式重定向；用 `CurrentSessionLogger` / `CurrentClassLogger`（NLog）即可。
- **跨平台路径**：前端用 `Extensions.toUnixPath`，后端在 LSP URI 处理时使用 `ToPlatformPath()`（`DotRush.Common.Extensions`），不要硬编码 `\\` 或 `/`。
- **EditorConfig 中已经关闭多个 IDE/style 分析器**（如 `IDE0290`/`IDE0066`/`IDE0028`/`CA1822` 等）。提交前别花时间修这些规则，它们是有意 silent 的；`Performance`/`Security` 类是 warning 级别需要保持干净。
- **VS Code 配置项命名**：扩展前端常量集中在 `src/VSCode/resources/constants.ts`，所有 setting key 通过 `Extensions.getSetting(res.configIdXxx)` 读，避免散布字符串字面量。
- **Submodule**：本地编辑 `DotRush.LanguageServer.Framework` / `DotRush.Debugging.MonoLib` 时记得它们是独立 git 仓库，commit 走各自的 upstream。

## Configuration Cheatsheet

LSP 行为既受 VS Code 设置控制（`dotrush.roslyn.*` 命名空间，定义在 `package.json`），也支持工作区根目录下的 `dotrush.config.json` 文件（结构形如 `{ "dotrush": { "roslyn": { ... } } }`）。后者由 `ConfigurationService` 在启动时读取，常用于 Neovim/Zed/Sublime 等无法走 VSCode 设置的环境。完整选项参见 `package.json` 的 `contributes.configuration`。
