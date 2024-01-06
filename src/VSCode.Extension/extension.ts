import { ContextMenuController } from './contextMenuController';
import { WizardController } from './wizards/wizardController';
import { ServerController } from './serverController';
import * as res from './resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export async function activate(context: vscode.ExtensionContext) {
	if (vscode.extensions.getExtension(res.extensionMicrosoftId)) {
		vscode.window.showErrorMessage(res.messageOmniSharpAlreadyInstalled);
		return;
	}
	if (vscode.workspace.workspaceFolders?.length === 1 && vscode.workspace.workspaceFile === undefined) {
		const csprojFiles = await vscode.workspace.findFiles(path.join('**', '*.csproj'));
		if (csprojFiles != undefined && csprojFiles.length > 1) {
			const message = res.messageMultipleProjectFilesFound.replace('{0}', csprojFiles.length.toString());
			const result = await vscode.window.showWarningMessage(message, res.messageLoadAll);
			if (result !== res.messageLoadAll)
				return;
		}
	}

	ContextMenuController.activate(context);
	WizardController.activate(context);
	ServerController.activate(context);
}
export function deactivate() {
	ServerController.stop();
}