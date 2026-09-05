import { DebugAdapterController } from '../controllers/debugAdapterController';
import { initializeComponents } from './traceView.html';
import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class TraceView implements vscode.CustomReadonlyEditorProvider<TraceDocument> {
    public static feature: TraceView = new TraceView();
    private processId: number | undefined;

    public activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.window.registerCustomEditorProvider('dotrush.traceView', this, {
            webviewOptions: { retainContextWhenHidden: true }
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdAttachTraceProfiler, async () => {
            const processId = this.processId ?? await vscode.commands.executeCommand(res.commandIdPickProcess);
            if (processId !== undefined)
                return vscode.tasks.executeTask(DotNetTaskProvider.getTraceTask(processId));
        }));

        context.subscriptions.push(DebugAdapterController.tracker.onProcessStarted((pid: number) => {
            this.processId = pid;
        }));
        context.subscriptions.push(DebugAdapterController.tracker.onSessionExited(() => {
            this.processId = undefined;
        }));
    }

    openCustomDocument(uri: vscode.Uri): TraceDocument {
        return new TraceDocument(uri);
    }
    resolveCustomEditor(document: TraceDocument, webviewPanel: vscode.WebviewPanel): void {
        const speedscopeRoot = vscode.Uri.parse(path.join(Interop.webviewsPath, 'speedscope'));
        webviewPanel.webview.options = {
            enableScripts: true,
            localResourceRoots: [speedscopeRoot, vscode.Uri.joinPath(document.uri, '..')]
        };
        webviewPanel.webview.onDidReceiveMessage((message: { type: string, message: string }) => {
            if (message.type === 'error')
                vscode.window.showErrorMessage(`${message.type}: ${message.message}`);
        });
        webviewPanel.webview.html = initializeComponents(webviewPanel.webview, speedscopeRoot, document.uri);
    }
}

class TraceDocument implements vscode.CustomDocument {
    constructor(public readonly uri: vscode.Uri) { }
    dispose(): void { }
}
