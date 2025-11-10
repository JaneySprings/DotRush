import { DebugAdapterController } from '../controllers/debugAdapterController';
import { LaunchProfile } from '../models/profile';
import { Extensions } from '../extensions';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';
import * as os from 'os';
import { spawn, execSync } from "child_process";

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
        if (config.request !== "launch" || consoleOption === "internalConsole") return;

        const program = config.program ?? "";
        const args = Array.isArray(config.args) ? config.args : (config.args ? [config.args] : []);
        const cwd = config.cwd ?? vscode.workspace.workspaceFolders?.[0]?.uri.fsPath ?? ".";
        const env = { ...process.env, ...(config.env ?? {}) };

        const pid = consoleOption === "integratedTerminal"
            ? await this.launchInIntegratedTerminal(program, args, cwd, env)
            : await this.launchInExternalTerminal(program, args, cwd, env);

        if (!pid) {
            vscode.window.showErrorMessage("DotRush: could not determine PID of launched process.");
            return;
        }

        const alive = await this.waitForProcessAlive(pid, 30, 200);
        if (!alive) {
            vscode.window.showErrorMessage(`DotRush: process ${pid} exited before debugger attached.`);
            return;
        }

        config.request = "attach";
        config.processId = pid;
        delete config.program;
        delete config.args;
    }

    /* ---------- Private helpers ---------- */

    private static async launchInIntegratedTerminal(
        program: string, args: string[], cwd: string, env: NodeJS.ProcessEnv
    ): Promise<number | undefined> {
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
            const pid = await terminal.processId;
            if (pid) return pid;
            await new Promise(r => setTimeout(r, 200));
        }
        return undefined;
    }
    private static async launchInExternalTerminal(
        program: string, args: string[], cwd: string, env: NodeJS.ProcessEnv
    ): Promise<number | undefined> {
        const platformName = os.platform();

        // Read external terminal path from settings.json
        let execPath =
            vscode.workspace.getConfiguration("terminal.external").get<string>(`${platformName}Exec`) ??
            vscode.workspace.getConfiguration("terminal.external", null).get<string>(`${platformName}Exec`);

        // Fallbacks
        if (!execPath) {
            if (platformName === "win32") execPath = "cmd.exe";
            else if (platformName === "linux") execPath = "/usr/bin/x-terminal-emulator";
            else if (platformName === "darwin") execPath = "open";
        }

        if (!execPath) {
            vscode.window.showErrorMessage(`DotRush: No external terminal configured for platform ${platformName}.`);
            return;
        }

        const terminalArgs = this.getExternalTerminalArgs(platformName, program, args);
        const childProc = spawn(execPath, terminalArgs, { cwd, env, detached: true, stdio: "ignore" });
        childProc.unref();

        // Wait for process to start
        await new Promise(r => setTimeout(r, 1500));
        return this.lookupDotnetPid(program, platformName);
    }
    private static getExternalTerminalArgs(platformName: string, program: string, args: string[]): string[] {
        if (platformName === "win32")
            return ["/c", "start", "powershell.exe", "-NoExit", "dotnet", `"${program}"`, ...args];
        if (platformName === "darwin")
            return ["-a", "Terminal.app", "--args", "dotnet", program, ...args];
        return ["--", "dotnet", program, ...args]; // Linux
    }
    private static lookupDotnetPid(program: string, platformName: string): number | undefined {
        try {
            if (platformName === "win32") {
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
                        if (pidMatch) return parseInt(pidMatch[1], 10);
                    }
                }
            } else {
                const output = execSync("ps -eo pid,command", { encoding: "utf8" });
                for (const line of output.split(/\r?\n/)) {
                    if (line.includes("dotnet") && line.includes(program)) {
                        const pidMatch = line.trim().match(/^(\d+)/);
                        if (pidMatch) return parseInt(pidMatch[1], 10);
                    }
                }
            }
        } catch (err) {
            vscode.window.showErrorMessage(`DotRush: PID lookup failed: ${(err as Error).message}`);
        }
        return undefined;
    }
    private static async waitForProcessAlive(pid: number, checks: number, delayMs: number): Promise<boolean> {
        for (let i = 0; i < checks; i++) {
            try {
                process.kill(pid, 0);
                await new Promise(r => setTimeout(r, delayMs));
            } catch {
                return false;
            }
        }
        return true;
    }
}