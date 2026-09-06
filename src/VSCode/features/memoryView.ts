import { DebugAdapterController } from '../controllers/debugAdapterController';
import { initializeComponents } from './memoryView.html';
import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class MemoryView implements vscode.CustomReadonlyEditorProvider<MemoryDocument> {
    public static feature: MemoryView = new MemoryView();
    private processId: number | undefined;

    public activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.window.registerCustomEditorProvider('dotrush.memoryView', this, {
            webviewOptions: { retainContextWhenHidden: true }
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdCreateHeapDump, async () => {
            const processId = this.processId ?? await vscode.commands.executeCommand(res.commandIdPickProcess);
            if (processId !== undefined)
                return vscode.tasks.executeTask(DotNetTaskProvider.getGCDumpTask(processId));
        }));

        context.subscriptions.push(DebugAdapterController.tracker.onProcessStarted((pid: number) => {
            this.processId = pid;
        }));
        context.subscriptions.push(DebugAdapterController.tracker.onSessionExited(() => {
            this.processId = undefined;
        }));
    }

    openCustomDocument(uri: vscode.Uri): MemoryDocument {
        return new MemoryDocument(uri);
    }
    resolveCustomEditor(document: MemoryDocument, webviewPanel: vscode.WebviewPanel): void {
        const memoryViewerRoot = vscode.Uri.parse(path.join(Interop.webviewsPath, 'memoryview'));
        const resourceRoots = [memoryViewerRoot, vscode.Uri.joinPath(document.uri, '..')];
        webviewPanel.webview.options = { enableScripts: true, localResourceRoots: resourceRoots };
        webviewPanel.webview.onDidReceiveMessage(async (message: { type: string, message?: string, requestId?: number }) => {
            if (message.type === 'error')
                vscode.window.showErrorMessage(`${message.type}: ${message.message}`);
            if (message.type === 'pickSnapshot') {
                const picked = await vscode.window.showOpenDialog({
                    title: res.messageSelectBaselineSnapshot,
                    canSelectMany: false,
                    defaultUri: vscode.Uri.joinPath(document.uri, '..'),
                    filters: { 'Heap snapshot': ['json'] }
                });
                const snapshot = picked?.[0];
                if (snapshot !== undefined) {
                    // The baseline may live outside the folder of the opened file, so allow the webview to fetch from there too
                    resourceRoots.push(vscode.Uri.joinPath(snapshot, '..'));
                    webviewPanel.webview.options = { enableScripts: true, localResourceRoots: resourceRoots };
                }
                webviewPanel.webview.postMessage({
                    type: 'snapshotPicked',
                    requestId: message.requestId,
                    url: snapshot === undefined ? undefined : webviewPanel.webview.asWebviewUri(snapshot).toString(),
                    fileName: snapshot === undefined ? undefined : path.basename(snapshot.fsPath)
                });
            }
        });
        webviewPanel.webview.html = initializeComponents(webviewPanel.webview, memoryViewerRoot, document.uri);
    }
}

class MemoryDocument implements vscode.CustomDocument {
    constructor(public readonly uri: vscode.Uri) { }
    dispose(): void { }
}
