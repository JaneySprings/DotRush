import { DebugAdapterController } from '../controllers/debugAdapterController';
import { initializeComponents } from './performanceView.html';
import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as rpc from 'vscode-jsonrpc/node';

export class PerformanceView implements vscode.WebviewViewProvider {
    public static feature: PerformanceView = new PerformanceView();

    private samples: UsageSample[] = [];
    private webviewView: vscode.WebviewView | undefined;
    private samplerConnection: rpc.MessageConnection | undefined;
    private processId: number | undefined;
    private readonly viewDurationSeconds = 60;

    public activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.window.registerWebviewViewProvider(res.extendedViewIdPerformance, this));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdAttachTraceProfiler, async () => {
            const processId = this.processId ?? await vscode.commands.executeCommand(res.commandIdPickProcess);
            if (processId !== undefined)
                return vscode.tasks.executeTask(DotNetTaskProvider.getTraceTask(processId));
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdCreateHeapDump, async () => {
            const processId = this.processId ?? await vscode.commands.executeCommand(res.commandIdPickProcess);
            if (processId !== undefined)
                return vscode.tasks.executeTask(DotNetTaskProvider.getGCDumpTask(processId));
        }));

        context.subscriptions.push(DebugAdapterController.tracker.onProcessStarted((pid: number) => {
            this.samples = [];
            this.processId = pid;
            this.startSampler(pid);
            this.postState();
        }));
        context.subscriptions.push(DebugAdapterController.tracker.onTargetExited(() => {
            this.processId = undefined;
            this.stopSampler();
            this.postState();
        }));
    }

    resolveWebviewView(webviewView: vscode.WebviewView): void {
        this.webviewView = webviewView;
        webviewView.webview.options = { enableScripts: true };
        webviewView.webview.html = initializeComponents(this.viewDurationSeconds);
        webviewView.onDidDispose(() => {
            if (this.webviewView === webviewView)
                this.webviewView = undefined;
        });
        webviewView.onDidChangeVisibility(() => {
            if (webviewView.visible)
                this.postState();
        });
        this.postState();
    }

    private startSampler(processId: number) {
        if (this.samplerConnection !== undefined)
            return;
        const connection = Interop.createDevHostRpc('sample', builder => builder.append('-p', processId.toString()));
        this.samplerConnection = connection;
        connection.onNotification('handleUsageSample', (sample: any) => {
            this.samples.push({ timestamp: Date.now(), workingSet: sample.workingSet, cpuUsage: sample.cpuUsage });
            const cutoff = Date.now() - (this.viewDurationSeconds + 5) * 1000;
            while (this.samples.length > 0 && this.samples[0].timestamp < cutoff)
                this.samples.shift();
            this.postState();
        });
    }
    private stopSampler() {
        const connection = this.samplerConnection;
        if (connection === undefined)
            return;
        this.samplerConnection = undefined;
        connection.sendNotification('handleSamplingStop').then(() => connection.dispose(), () => connection.dispose());
    }
    private postState() {
        if (this.webviewView !== undefined && this.webviewView.visible)
            this.webviewView.webview.postMessage({ samples: this.samples, frozen: this.processId === undefined });
    }
}

interface UsageSample {
    timestamp: number;
    workingSet: number;
    cpuUsage?: number;
}
