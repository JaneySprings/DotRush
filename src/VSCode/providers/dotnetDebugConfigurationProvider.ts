import { DebugAdapterController } from '../controllers/debugAdapterController';
import { LaunchProfile } from '../models/profile';
import { Extensions } from '../extensions';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

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
            : DotNetDebugConfigurationProvider.provideNcdbgConfiguration(config);

        if (config.request === 'launch' && !config.program)
            config.program = await vscode.commands.executeCommand(res.commandIdActiveTargetPath);
        if (config.request === 'attach' && !config.processId && !config.processName && !config.processPath)
            config.processId = await vscode.commands.executeCommand(res.commandIdPickProcess);

        if (!config.cwd && config.program)
            config.cwd = path.dirname(config.program);

        return config;
    }

    private static provideVsdbgConfiguration(config: vscode.DebugConfiguration) {
        if (config.launchSettingsFilePath === undefined && config.request === 'launch')
            config.launchSettingsFilePath = DebugAdapterController.getLaunchSettingsPath();

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
    }
    private static provideNcdbgConfiguration(config: vscode.DebugConfiguration) {
        if (config.launchSettingsFilePath === undefined && config.request === 'launch')
            config.launchSettingsFilePath = DebugAdapterController.getLaunchSettingsPath();
        if (config.launchSettingsFilePath !== undefined) { /* https://github.com/JaneySprings/DotRush/issues/22 */
            const profile = DebugAdapterController.getLaunchProfile(config.launchSettingsFilePath, config.launchSettingsProfile);
            DotNetDebugConfigurationProvider.provideNcdbgConfigurationFromProfile(config, profile);
        }

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
    }
    private static provideNcdbgConfigurationFromProfile(config: vscode.DebugConfiguration, profile: LaunchProfile | undefined) {
        if (profile === undefined)
            return config;

        if (profile.workingDirectory !== undefined)
            config.cwd = profile.workingDirectory;
        if (profile.executablePath !== undefined)
            config.program = profile.executablePath;
        if (profile.environmentVariables !== undefined)
            config.env = profile.environmentVariables;
        if (profile.commandLineArgs !== undefined)
            config.args = [profile.commandLineArgs]; //TODO: We need to split the command line args

        if (profile.applicationUrl !== undefined)
            config.env = { ...config.env, ASPNETCORE_URLS: profile.applicationUrl };
    }
}