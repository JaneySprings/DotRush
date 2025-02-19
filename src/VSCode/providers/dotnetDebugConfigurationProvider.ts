import { DebugAdapterController } from '../controllers/debugAdapterController';
import { StatusBarController } from '../controllers/statusbarController';
import { Extensions } from '../extensions';
import { Interop } from '../interop/interop';
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

		if (!config.program && config.request === 'launch')
			config.program = await DotNetDebugConfigurationProvider.getProgramPath();
		if (!config.processId && config.request === 'attach')
			config.processId = await DebugAdapterController.showQuickPickProcess();

        return DotNetDebugConfigurationProvider.provideDebuggerOptions(config);
	}

	private static provideDebuggerOptions(options: vscode.DebugConfiguration): vscode.DebugConfiguration {
        if (options.justMyCode === undefined)
            options.justMyCode = Extensions.getSetting('debugger.projectAssembliesOnly', false);
        if (options.enableStepFiltering === undefined)
            options.enableStepFiltering = Extensions.getSetting('debugger.stepOverPropertiesAndOperators', false);
        if (options.console === undefined)
            options.console = Extensions.getSetting('debugger.console');
        if (options.symbolOptions === undefined)
            options.symbolOptions = {
                searchPaths: Extensions.getSetting('debugger.symbolSearchPaths'),
                searchMicrosoftSymbolServer: Extensions.getSetting('debugger.searchMicrosoftSymbolServer', false),
            };
        if (options.sourceLinkOptions === undefined)
            options.sourceLinkOptions = {
                "*": { enabled: Extensions.getSetting('debugger.automaticSourcelinkDownload', true) }
            }

        return options;
    }
    
	private static async getProgramPath(): Promise<string | undefined> {
        if (StatusBarController.activeProject === undefined || StatusBarController.activeConfiguration === undefined)
            return await DebugAdapterController.showQuickPickProgram();

        const assemblyPath = Interop.getPropertyValue('TargetPath', StatusBarController.activeProject.path, StatusBarController.activeConfiguration, StatusBarController.activeFramework);
		if (!assemblyPath)
			return await DebugAdapterController.showQuickPickProgram();

        const programDirectory = path.dirname(assemblyPath);
        const programFile = path.basename(assemblyPath, '.dll');
        const programPath = path.join(programDirectory, programFile + Interop.execExtension);
		return programPath;
	}
}