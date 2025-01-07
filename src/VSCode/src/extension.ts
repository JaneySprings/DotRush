import { ContextMenuController } from './controllers/contextMenuController';
import { DebugAdapterController } from './controllers/debugAdapterController';
import { LanguageServerController } from './controllers/languageServerController';
import { StateController } from './controllers/stateController';
import { StatusBarController } from './controllers/statusbarController';
import { TestExplorerController } from './controllers/testExplorerController';
import { ModulesView } from './features/modulesView';
import { Interop } from './interop/interop';
import * as res from './resources/constants';
import * as vscode from 'vscode';

export async function activate(context: vscode.ExtensionContext) {
	if (vscode.extensions.getExtension(res.extensionMicrosoftId)) {
		vscode.window.showErrorMessage(res.messageOmniSharpAlreadyInstalled, { modal: true });
		return;
	}

	Interop.initialize(context.extensionPath);

	await StateController.activate(context);
	await StatusBarController.activate(context);
	await ContextMenuController.activate(context);
	await DebugAdapterController.activate(context);
	await LanguageServerController.activate(context);
	await TestExplorerController.activate(context);

	ModulesView.feature.activate(context);
}
export function deactivate() {
	StateController.deactivate();
	LanguageServerController.stop();
}