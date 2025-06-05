import { ContextMenuController } from './controllers/contextMenuController';
import { DebugAdapterController } from './controllers/debugAdapterController';
import { LanguageServerController } from './controllers/languageServerController';
import { StateController } from './controllers/stateController';
import { StatusBarController } from './controllers/statusbarController';
import { TestExplorerController } from './controllers/testExplorerController';
import { ExternalTypeResolver } from './features/externalTypeResolver';
import { ModulesView } from './features/modulesView';
import { PerformanceView } from './features/performanceView';
import { Interop } from './interop/interop';
import { PublicExports } from './publicExports';
import * as vscode from 'vscode';

export async function activate(context: vscode.ExtensionContext) {
	const exports = new PublicExports();
	Interop.initialize(context.extensionPath);

	if (vscode.workspace.workspaceFolders !== undefined) {
		StateController.activate(context);
		StatusBarController.activate(context);
		ContextMenuController.activate(context);
		TestExplorerController.activate(context);
		LanguageServerController.activate(context);
	}

	DebugAdapterController.activate(context);
	ModulesView.feature.activate(context);
	PerformanceView.feature.activate(context);
	ExternalTypeResolver.feature.activate(context);

	return exports;
}
export function deactivate() {
	StateController.deactivate();
	LanguageServerController.stop();
}