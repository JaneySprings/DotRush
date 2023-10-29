import { CommandsController } from './commands';
import { ClientController } from './client';
import * as res from './resources';
import * as vscode from 'vscode';


export function activate(context: vscode.ExtensionContext) {
	if (vscode.extensions.getExtension(res.extensionMicrosoftId)) {
		vscode.window.showErrorMessage(res.messageOmniSharpAlreadyInstalled);
		return;
	}

	CommandsController.activate(context);
	ClientController.activate(context);
}

export function deactivate() {
	ClientController.stop();
}