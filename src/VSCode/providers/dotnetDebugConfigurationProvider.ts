import { DebugAdapterController } from '../controllers/debugAdapterController';
import { LaunchProfile } from '../models/profile';
import { Extensions } from '../extensions';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class DotNetDebugConfigurationProvider implements vscode.DebugConfigurationProvider {
	async resolveDebugConfiguration(folder: vscode.WorkspaceFolder | undefined,
									config: vscode.DebugConfiguration, 
									token?: vscode.CancellationToken): Promise<vscode.DebugConfiguration | undefined> {

		if (!config.type && !config.request && !config.name) {
			config.name = res.debuggerVsdbgTitle;
			config.type = res.debuggerVsdbgId;
			config.request = folder === undefined ? 'attach' : 'launch';
			config.preLaunchTask = folder === undefined ? undefined : `${res.extensionId}: Build`;
		}

        await DotNetDebugConfigurationProvider.provideDebuggerConfiguration(config);

		if (!config.program && config.request === 'launch')
			config.program = await DebugAdapterController.getProgramPath();
		if (!config.processId && config.request === 'attach')
			config.processId = await DebugAdapterController.showQuickPickProcess();

        if (!config.cwd && config.program)
            config.cwd = path.dirname(config.program);

        return config;
	}

	private static async provideDebuggerConfiguration(config: vscode.DebugConfiguration) {
        if (config.launchSettingsFilePath !== undefined && Extensions.onVSCode(false, true)) {
            const profile = await DebugAdapterController.getLaunchProfile(config.launchSettingsFilePath, config.launchSettingsProfile);
            DotNetDebugConfigurationProvider.provideDebuggerConfigurationFromProfile(config, profile);
        }

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
    private static provideDebuggerConfigurationFromProfile(config: vscode.DebugConfiguration, profile: LaunchProfile | undefined) {
        if (profile === undefined)
            return config;

        config.cwd = profile.workingDirectory;
        config.program = profile.executablePath;
        config.args = profile.commandLineArgs;
        config.env = profile.environmentVariables;

        if (profile.applicationUrl !== undefined)
            config.env = { ...config.env, ASPNETCORE_URLS: profile.applicationUrl };
    }
}