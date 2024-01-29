import { ContextMenuController } from './contextMenuController';
import { WizardController } from './wizards/wizardController';
import { WorkspaceController } from './workspaceController';
import { ServerController } from './serverController';
import * as res from './resources/constants';
import * as vscode from 'vscode';

export async function activate(context: vscode.ExtensionContext) {
	if (vscode.extensions.getExtension(res.extensionMicrosoftId)) {
		vscode.window.showErrorMessage(res.messageOmniSharpAlreadyInstalled, { modal: true });
		return;
	}

	WizardController.activate(context);
	ServerController.activate(context);
	ContextMenuController.activate(context);
	WorkspaceController.activate(context);
}
export function deactivate() {
	ServerController.stop();
}