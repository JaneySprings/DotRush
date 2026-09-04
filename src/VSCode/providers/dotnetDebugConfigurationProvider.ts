import { DebugAdapterController } from '../controllers/debugAdapterController';
import { LaunchProfile } from '../models/profile';
import { Extensions } from '../extensions';
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

        DotNetDebugConfigurationProvider.provideCommonConfiguration(config);

        if (config.request === 'launch' && !config.program)
            config.program = await vscode.commands.executeCommand(res.commandIdActiveTargetPath);
        if (config.request === 'attach' && !config.processId && !config.processName)
            config.processId = await vscode.commands.executeCommand(res.commandIdPickProcess);

        if (!config.cwd && config.program)
            config.cwd = path.dirname(config.program);

        return config;
    }


    private static provideCommonConfiguration(config: vscode.DebugConfiguration) {
        let profile: LaunchProfile | undefined = undefined;
        if (config.launchSettingsFilePath === undefined && config.request === 'launch')
            config.launchSettingsFilePath = DebugAdapterController.getLaunchSettingsPath();
        if (config.launchSettingsFilePath !== undefined)
            profile = DebugAdapterController.getLaunchProfile(config.launchSettingsFilePath, config.launchSettingsProfile);

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
}