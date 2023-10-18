import { CommandsController } from './commands';
import { RuntimeController } from './selector';
import { ClientController } from './client';
import * as res from './resources';
import * as vscode from 'vscode';
import * as path from 'path';


export function activate(context: vscode.ExtensionContext) {
	if (vscode.extensions.getExtension(res.extensionMicrosoftId)) {
		vscode.window.showErrorMessage(res.messageOmniSharpAlreadyInstalled);
		return;
	}

	if (!RuntimeController.activate(context)) 
		return;

	CommandsController.activate(context);
	ClientController.activate(context);

	context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async ev => {
		if (!ev.fileName.endsWith('.csproj'))
			return;

		const message = res.messageProjectChanged.replace('{0}', path.basename(ev.fileName));
		const result = await vscode.window.showWarningMessage(message, res.messageReload);
		if (result !== undefined)
			vscode.commands.executeCommand(res.commandIdReloadWindow);
	}));
}

export function deactivate() {
	ClientController.stop();
}

export function getSetting<T>(option: string): T | undefined { 
	return vscode.workspace.getConfiguration(res.extensionId).get<T>(option);
}