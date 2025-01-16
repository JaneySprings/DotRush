import { DebugAdapterController } from '../controllers/debugAdapterController';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

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
			config.program = await DebugAdapterController.getProgramPath();
		if (!config.processId && config.request === 'attach')
			config.processId = await DebugAdapterController.getProcessId();

        return DebugAdapterController.provideDebuggerOptions(config);
	}
}