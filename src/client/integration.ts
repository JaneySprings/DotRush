import * as vscode from 'vscode';


export function assumePluginUninstalled(id: string): boolean {
	if (vscode.extensions.getExtension(id) === undefined)
		return true;
    vscode.window.showErrorMessage(`Extension ${id} is already installed.`);
	return false;
}

export async function waitForActivation(id: string, timeout: number = 100): Promise<vscode.Extension<any> | undefined> {
    const extension = vscode.extensions.getExtension(id);
	if (extension === undefined)
		return undefined;

	let retryCount = 0;
	while (!extension?.isActive) {
        retryCount++;
		await new Promise(f => setTimeout(f, 100));
		if (retryCount > timeout)
			break;
	}

    if (extension?.isActive)
        return extension;

    return undefined;
}