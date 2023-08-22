import { RuntimeController } from './selector';
import { ClientController } from './client';
import * as res from './resources';
import * as vscode from 'vscode';


export function activate(context: vscode.ExtensionContext) {
	if (vscode.extensions.getExtension(res.extensionMicrosoftId)) {
		vscode.window.showErrorMessage(res.messageOmniSharpAlreadyInstalled);
		return;
	}

	if (RuntimeController.activate(context))
		ClientController.activate(context);
}

export function deactivate() {
	ClientController.stop();
}

export function getSetting<T>(option: string): T | undefined { 
	return vscode.workspace.getConfiguration(res.extensionId).get<T>(option);
}