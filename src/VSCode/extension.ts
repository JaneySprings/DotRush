import { ContextMenuController } from './controllers/contextMenuController';
import { DebugAdapterController } from './controllers/debugAdapterController';
import { LanguageServerController } from './controllers/languageServerController';
import { StateController } from './controllers/stateController';
import { StatusBarController } from './controllers/statusbarController';
import { TestExplorerController } from './controllers/testExplorerController';
import { ModulesView } from './features/modulesView';
import { Interop } from './interop/interop';
import { PublicExports } from './publicExports';
import * as res from './resources/constants';
import * as vscode from 'vscode';

export async function activate(context: vscode.ExtensionContext) {
	if (vscode.extensions.getExtension(res.extensionMicrosoftId)) {
		vscode.window.showErrorMessage(res.messageOmniSharpAlreadyInstalled, { modal: true });
		return;
	}

	const exports = new PublicExports();
	Interop.initialize(context.extensionPath);

	await StateController.activate(context);
	await StatusBarController.activate(context);
	ContextMenuController.activate(context);
	DebugAdapterController.activate(context);
	LanguageServerController.activate(context);
	TestExplorerController.activate(context);

	ModulesView.feature.activate(context);

	return exports;
}
export function deactivate() {
	StateController.deactivate();
	LanguageServerController.stop();
}