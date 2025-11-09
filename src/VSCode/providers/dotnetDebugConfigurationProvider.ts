import { DebugAdapterController } from '../controllers/debugAdapterController';
import { LaunchProfile } from '../models/profile';
import { Extensions } from '../extensions';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

import { spawn, execSync } from "child_process";
import * as os from "os";
import * as psList from "ps-list";
import * as psTree from "ps-tree";

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
        if (config.request !== "launch" || consoleOption === "internalConsole") {
            return;
        }

        const program = config.program ?? "";
        const args = Array.isArray(config.args) ? config.args : (config.args ? [config.args] : []);
        const cwd = config.cwd ?? vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? ".";
        const env = { ...process.env, ...(config.env ?? {}) };

        let pid: number | undefined;

        if (consoleOption === "integratedTerminal") {
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
        } else {
            const { spawn, execSync } = await import("child_process");
            const { platform } = await import("os");

            const childProc = spawn("dotnet", [program, ...args], {
                cwd,
                env,
                detached: true,
                stdio: "ignore",
                shell: true
            });

            childProc.unref();

            // Wait for the app to start before PID lookup
            await new Promise(r => setTimeout(r, 1500));

            try {
                if (platform() === "win32") {
                    const output = execSync(
                        "wmic process where \"name='dotnet.exe'\" get ProcessId,CommandLine /format:list",
                        { encoding: "utf8" }
                    );
                    const lines = output.split(/\r?\n/).map(l => l.trim());
                    for (let i = 0; i < lines.length; i++) {
                        const line = lines[i];
                        if (line.startsWith("CommandLine=") &&
                            line.toLowerCase().includes(program.toLowerCase())) {
                            const pidLine = lines[i + 1];
                            const pidMatch = pidLine?.match(/ProcessId=(\d+)/);
                            if (pidMatch) {
                                pid = parseInt(pidMatch[1], 10);
                                break;
                            }
                        }
                    }
                } else {
                    const output = execSync("ps -eo pid,command", { encoding: "utf8" });
                    const lines = output.split(/\r?\n/);
                    for (const line of lines) {
                        if (line.includes("dotnet") && line.includes(program)) {
                            const pidMatch = line.trim().match(/^(\d+)/);
                            if (pidMatch) {
                                pid = parseInt(pidMatch[1], 10);
                                break;
                            }
                        }
                    }
                }
            } catch {
                // ignore lookup errors
            }
        }

        if (!pid) {
            vscode.window.showErrorMessage("DotRush: could not determine PID of launched process.");
            return;
        }

        // Ensure process is alive before attaching
        for (let i = 0; i < 30; i++) {
            try {
                process.kill(pid, 0);
                await new Promise(r => setTimeout(r, 200));
            } catch {
                vscode.window.showErrorMessage(`DotRush: process ${pid} exited before debugger attached.`);
                return;
            }
        }

        // Convert to attach configuration
        config.request = "attach";
        config.processId = pid;
        delete config.program;
        delete config.args;
    }
}