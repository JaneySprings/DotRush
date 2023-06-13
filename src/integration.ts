import * as vscode from 'vscode';


export function assumePluginUninstalled(id: string): boolean {
	if (vscode.extensions.getExtension(id) === undefined)
		return true;
    vscode.window.showErrorMessage(`Extension ${id} is already installed.`);
	return false;
}