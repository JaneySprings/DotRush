import { ServerController } from './serverController';
import * as res from './constants';
import * as vscode from 'vscode';
import * as path from 'path';

export async function activate(context: vscode.ExtensionContext) {
	if (vscode.extensions.getExtension(res.extensionMicrosoftId)) {
		vscode.window.showErrorMessage(res.messageOmniSharpAlreadyInstalled, { modal: true });
		return;
	}

	ServerController.activate(context);

	context.subscriptions.push(vscode.commands.registerCommand(res.commandIdRestartServer, () => ServerController.restart()));
	context.subscriptions.push(vscode.workspace.onDidChangeWorkspaceFolders(() => ServerController.restart()));
	context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async e => {
		if (path.extname(e.fileName) !== '.csproj')
			return;

		const value = await vscode.window.showWarningMessage(res.messageProjectChanged, res.messageReload)
		if (value === res.messageReload)
			ServerController.restart();
	}));
}
export function deactivate() {
	ServerController.stop();
}