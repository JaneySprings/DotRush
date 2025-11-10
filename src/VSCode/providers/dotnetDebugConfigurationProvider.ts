import { DebugAdapterController } from '../controllers/debugAdapterController';
import { LaunchProfile } from '../models/profile';
import { Extensions } from '../extensions';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';
import * as os from 'os';
import { spawn, execSync } from "child_process";
import { log } from '../logging/logger';

export class DotNetDebugConfigurationProvider implements vscode.DebugConfigurationProvider {
    public async resolveDebugConfiguration(
        folder: vscode.WorkspaceFolder | undefined,
        config: vscode.DebugConfiguration,
        token?: vscode.CancellationToken): Promise<vscode.DebugConfiguration> {

        if (!config.type && !config.request && !config.name) {
            config.name = res.debuggerNetCoreTitle;
            config.type = res.debuggerNetCoreId;
            config.request = folder === undefined ? 'attach' : 'launch';
            config.preLaunchTask = folder === undefined ? undefined : `${res.extensionId}: Build`;
        }

        return config;
    }
    public async resolveDebugConfigurationWithSubstitutedVariables(
        folder: vscode.WorkspaceFolder | undefined,
        config: vscode.DebugConfiguration,
        token?: vscode.CancellationToken): Promise<vscode.DebugConfiguration> {

        Extensions.onVSCode(true, false)
            ? DotNetDebugConfigurationProvider.provideVsdbgConfiguration(config)
            : await DotNetDebugConfigurationProvider.provideNcdbgConfiguration(config);

        if (config.request === 'launch' && !config.program)
            config.program = await vscode.commands.executeCommand(res.commandIdActiveTargetPath);
        if (config.request === 'attach' && !config.processId && !config.processName && !config.processPath)
            config.processId = await vscode.commands.executeCommand(res.commandIdPickProcess);

        if (!config.cwd && config.program)
            config.cwd = path.dirname(config.program);

        return config;
    }

    private static provideVsdbgConfiguration(config: vscode.DebugConfiguration) {
        let profile: LaunchProfile | undefined = undefined;
        if (config.launchSettingsFilePath === undefined && config.request === 'launch')
            config.launchSettingsFilePath = DebugAdapterController.getLaunchSettingsPath();
        if (config.launchSettingsFilePath !== undefined) {
            profile = DebugAdapterController.getLaunchProfile(config.launchSettingsFilePath, config.launchSettingsProfile);
        }

        DotNetDebugConfigurationProvider.provideCommonConfiguration(config, profile);
    }
    private static async provideNcdbgConfiguration(config: vscode.DebugConfiguration) {
        let profile: LaunchProfile | undefined = undefined;
        if (config.launchSettingsFilePath === undefined && config.request === 'launch')
            config.launchSettingsFilePath = DebugAdapterController.getLaunchSettingsPath();
        if (config.launchSettingsFilePath !== undefined) {
            profile = DebugAdapterController.getLaunchProfile(config.launchSettingsFilePath, config.launchSettingsProfile);
            /* https://github.com/JaneySprings/DotRush/issues/22 */
            if (profile?.workingDirectory !== undefined)
                config.cwd = profile.workingDirectory;
            if (profile?.executablePath !== undefined)
                config.program = profile.executablePath;
            if (profile?.environmentVariables !== undefined)
                config.env = profile.environmentVariables;
            if (profile?.commandLineArgs !== undefined)
                config.args = [profile.commandLineArgs]; //TODO: We need to split the command line args
            if (profile?.applicationUrl !== undefined)
                config.env = { ...config.env, ASPNETCORE_URLS: profile.applicationUrl };
        }

        DotNetDebugConfigurationProvider.provideCommonConfiguration(config, profile);

        // Handle console preferences cleanly only if we are in fork mode
        if(!Extensions.onVSCode(true, false)) {
            await DotNetDebugConfigurationProvider.handleConsolePreference(config);
        }
    }
    private static provideCommonConfiguration(config: vscode.DebugConfiguration, profile?: LaunchProfile) {
        // https://github.com/JaneySprings/DotRush/issues/39
        if (config.processPath !== undefined && config.request === 'attach')
            config.processId = Interop.createProcess(config.processPath)

        if (config.justMyCode === undefined)
            config.justMyCode = Extensions.getSetting(res.configIdDebuggerProjectAssembliesOnly, false);
        if (config.enableStepFiltering === undefined)
            config.enableStepFiltering = Extensions.getSetting(res.configIdDebuggerStepOverPropertiesAndOperators, false);
        if (config.console === undefined)
            config.console = Extensions.getSetting(res.configIdDebuggerConsole);
        if (config.symbolOptions === undefined)
            config.symbolOptions = {
                searchPaths: Extensions.getSetting(res.configIdDebuggerSymbolSearchPaths),
                searchMicrosoftSymbolServer: Extensions.getSetting(res.configIdDebuggerSearchMicrosoftSymbolServer, false),
            };
        if (config.sourceLinkOptions === undefined)
            config.sourceLinkOptions = {
                "*": { enabled: Extensions.getSetting(res.configIdDebuggerAutomaticSourcelinkDownload, true) }
            }
        if (config.launchWebBrowser === undefined)
            config.launchWebBrowser = Extensions.getSetting(res.configIdDebuggerLaunchBrowser, true);

        if (profile?.launchBrowser !== false && config.launchWebBrowser) { // launchBrowser already used by vsdbg (same logic as in vscode)
            config.serverReadyAction = { action: "openExternally", pattern: "\\bNow listening on:\\s+(https?://\\S+)" };
            if (profile?.launchUrl !== undefined)
                config.serverReadyAction.uriFormat = `%s/${profile.launchUrl}`;
        }
    }
    /**
     * Launches the target program in the appropriate console (integrated or external)
     * before attaching NetCoreDbg to it. This emulates vsdbg console behavior.
     */
    private static async handleConsolePreference(config: vscode.DebugConfiguration): Promise<void> {
        const consoleOption = config.console ?? "internalConsole";
        log(`handleConsolePreference: Starting with console option: ${consoleOption}, request: ${config.request}`);

        if (config.request !== "launch" || consoleOption === "internalConsole") {
            log(`handleConsolePreference: Skipping (request=${config.request}, console=${consoleOption})`);
            return;
        }

        const program = config.program ?? "";
        const args = Array.isArray(config.args) ? config.args : (config.args ? [config.args] : []);
        const cwd = config.cwd ?? vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? ".";
        const env = { ...process.env, ...(config.env ?? {}) };

        log(`handleConsolePreference: program="${program}", args=${JSON.stringify(args)}, cwd="${cwd}"`);

        let pid: number | undefined;

        if (consoleOption === "integratedTerminal") {
            log(`handleConsolePreference: Launching in integrated terminal`);
            // ✅ Integrated terminal: handled by VSCode
            const terminal = vscode.window.createTerminal({
                name: "DotRush (Integrated)",
                cwd,
                shellPath: "dotnet",
                shellArgs: [program, ...args],
                location: vscode.TerminalLocation.Panel,
                env
            });
            terminal.show();

            for (let i = 0; i < 10; i++) {
                pid = await terminal.processId;
                if (pid) break;
                await new Promise(r => setTimeout(r, 200));
            }
            log(`handleConsolePreference: Integrated terminal PID: ${pid}`);
        } else {
            log(`handleConsolePreference: Launching in external terminal (platform: ${os.platform()})`);


            const terminalConfig = vscode.workspace.getConfiguration("terminal.external");
            const execPath = terminalConfig.get<string>(`${os.platform()}Exec`);
            log(`handleConsolePreference: External terminal execPath: ${execPath}`);

            if (!execPath) {
                log(`handleConsolePreference: ERROR - No external terminal configured for platform ${os.platform()}`);
                vscode.window.showErrorMessage(`DotRush: No external terminal configured for platform ${os.platform()}.`);
                return;
            }

            // Build terminal launch command
            let terminalArgs: string[] = [];
            if (os.platform() === "win32") {
                terminalArgs = ["/c", "start", "cmd.exe", "/k", `dotnet "${program}" ${args.join(" ")}`];
            } else if (os.platform() === "darwin") {
                terminalArgs = ["-a", "Terminal.app", "--args", "dotnet", program, ...args];
            } else {
                // Linux → use -- to pass the command to the terminal
                terminalArgs = ["--", "dotnet", program, ...args];
            }

            log(`handleConsolePreference: Spawning external terminal with args: ${JSON.stringify(terminalArgs)}`);

            const childProc = spawn(execPath, terminalArgs, {
                cwd,
                env,
                detached: true,
                stdio: "ignore"
            });
            childProc.unref();

            // Wait for the app to start
            await new Promise(r => setTimeout(r, 1500));
            log(`handleConsolePreference: Process spawned, waiting for app to start. Now looking up PID...`);

            // Lookup PID of dotnet process (for attach)
            try {
                if (os.platform() === "win32") {
                    log(`handleConsolePreference: Looking up PID on Windows using wmic`);

                    const output = execSync(
                        "wmic process where \"name='dotnet.exe'\" get ProcessId,CommandLine /format:list",
                        { encoding: "utf8" }
                    );
                    const lines = output.split(/\r?\n/).map(l => l.trim());
                    for (let i = 0; i < lines.length; i++) {
                        const line = lines[i];
                        if (line.startsWith("CommandLine=") && line.toLowerCase().includes(program.toLowerCase())) {
                            const pidLine = lines[i + 1];
                            const pidMatch = pidLine?.match(/ProcessId=(\d+)/);
                            if (pidMatch) {
                                pid = parseInt(pidMatch[1], 10);
                                log(`handleConsolePreference: Found PID on Windows: ${pid}`);
                                break;
                            }
                        }
                    }
                } else {
                    log(`handleConsolePreference: Looking up PID on Unix/Linux using ps`);

                    const output = execSync("ps -eo pid,command", { encoding: "utf8" });
                    const lines = output.split(/\r?\n/);
                    for (const line of lines) {
                        if (line.includes("dotnet") && line.includes(program)) {
                            const pidMatch = line.trim().match(/^(\d+)/);
                            if (pidMatch) {
                                pid = parseInt(pidMatch[1], 10);
                                log(`handleConsolePreference: Found PID on Unix/Linux: ${pid}`);
                                break;
                            }
                        }
                    }
                }
            } catch (err) {
                log(`handleConsolePreference: ERROR - Failed to determine PID: ${(err as Error).message}`);
                vscode.window.showErrorMessage(`DotRush: could not determine PID of launched process: ${(err as Error).message}`);
            }
        }

        if (!pid) {
            log(`handleConsolePreference: ERROR - No PID found after lookup`);
            vscode.window.showErrorMessage("DotRush: could not determine PID of launched process. (No PID found)");
            return;
        }

        log(`handleConsolePreference: PID determined: ${pid}. Verifying process is alive...`);


        // Ensure process is alive before attach
        for (let i = 0; i < 30; i++) {
            try {
                process.kill(pid, 0);
                await new Promise(r => setTimeout(r, 200));
            } catch {
                log(`handleConsolePreference: ERROR - Process ${pid} exited before debugger could attach`);
                vscode.window.showErrorMessage(`DotRush: process ${pid} exited before debugger attached.`);
                return;
            }
        }

        log(`handleConsolePreference: Process ${pid} is alive and ready. Converting to attach configuration.`);

        // Convert to attach configuration
        config.request = "attach";
        config.processId = pid;
        delete config.program;
        delete config.args;

        log(`handleConsolePreference: Configuration converted to attach mode with PID ${pid}`);

    }
}