import { ContextMenuController } from './context';
import { assumePluginUninstalled } from './integration';
import { ClientController } from './client';
import * as res from './resources';
import * as vscode from 'vscode';


export function activate(context: vscode.ExtensionContext) {
	if (!assumePluginUninstalled(res.extensionMicrosoftId))
		return;

	ContextMenuController.activate(context);
	ClientController.activate(context);
}

export function deactivate() {
	ClientController.stop();
}


export function getSetting<T>(option: string): T | undefined { 
	const config = vscode.workspace.getConfiguration(res.extensionId);
	return config.get<T>(option);
}
