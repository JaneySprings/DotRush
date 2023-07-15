import { ContextMenuController } from './context';
import { ClientController } from './client';
import * as res from './resources';
import * as vscode from 'vscode';
import { RuntimeController } from './selector';


export function activate(context: vscode.ExtensionContext) {
	if (!assumePluginUninstalled(res.extensionMicrosoftId))
		return;

	if (!RuntimeController.activate(context))
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

function assumePluginUninstalled(id: string): boolean {
	if (vscode.extensions.getExtension(id) === undefined)
		return true;

    vscode.window.showErrorMessage(`Extension ${id} is already installed.`);
	return false;
}