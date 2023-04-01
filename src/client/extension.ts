import { ContextMenuController } from './context/contextMenuController';
import { assumePluginUninstalled } from './integration/extensionTools';
import { SolutionController } from './csharp/solutionController';
import { ClientController } from './csharp/clientController';
import * as res from './resources';
import * as vscode from 'vscode';


export function activate(context: vscode.ExtensionContext) {
	if (!assumePluginUninstalled(res.extensionMicrosoftId))
		return;

	ContextMenuController.activate(context);
	SolutionController.activate();
}

export function deactivate() {
	ClientController.stop();
}