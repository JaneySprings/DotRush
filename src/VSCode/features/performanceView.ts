import { DebugAdapterController } from '../controllers/debugAdapterController';
import { initializeComponents } from './performanceView.html';
import { ProcessArgumentBuilder } from '../interop/processArgumentBuilder';
import { ProcessRunner } from '../interop/processRunner';
import { Interop } from '../interop/interop';
import { ChildProcess } from 'child_process';
import * as vscode from 'vscode';
import * as path from 'path';

export class PerformanceView implements vscode.WebviewViewProvider {
    public static feature: PerformanceView = new PerformanceView();

    private samples: UsageSample[] = [];
    private webviewView: vscode.WebviewView | undefined;
    private countersProcess: ChildProcess | undefined;
    private readonly viewDurationSeconds = 60;

    public activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.window.registerWebviewViewProvider('dotrush.performanceView', this));
        context.subscriptions.push(DebugAdapterController.tracker.onProcessStarted((pid: number) => {
            this.samples = [];
            this.startSampler(pid);
            this.postState();
        }));
        context.subscriptions.push(DebugAdapterController.tracker.onSessionExited(() => {
            this.countersProcess?.kill();
            this.countersProcess = undefined;
            this.postState();
        }));
    }

    resolveWebviewView(webviewView: vscode.WebviewView): void {
        this.webviewView = webviewView;
        webviewView.webview.options = { enableScripts: true };
        webviewView.webview.html = initializeComponents(this.viewDurationSeconds);
        webviewView.onDidChangeVisibility(() => this.postState());
        webviewView.onDidDispose(() => {
            if (this.webviewView === webviewView)
                this.webviewView = undefined;
        });
        this.postState();
    }

    private startSampler(processId: number) {
        if (this.countersProcess !== undefined)
            return;

        const builder = new ProcessArgumentBuilder(Interop.dotnetPath)
            .append(path.join(Interop.binariesPath, 'Diagnostics', 'dotnet-counters.dll'))
            .append('collect', '-p', processId.toString(), '--format', 'jsonl')
            .append('--counters', 'EventCounters\\System.Runtime[cpu-usage,working-set,gc-heap-size,time-in-gc]');
        this.countersProcess = ProcessRunner.runStream<{ name: string, value: number }>(builder, counter => {
            // The counters of one interval arrive together and form a single sample
            let sample = this.samples[this.samples.length - 1];
            if (sample === undefined || Date.now() - sample.timestamp > 500)
                this.samples.push(sample = { timestamp: Date.now() });

            if (counter.name === 'cpu-usage')
                sample.cpuUsage = counter.value;
            if (counter.name === 'working-set')
                sample.workingSet = counter.value * 1_000_000; // Reported in MB
            if (counter.name === 'gc-heap-size')
                sample.gcHeapSize = counter.value * 1_000_000; // Reported in MB
            if (counter.name === 'time-in-gc')
                sample.timeInGC = counter.value;

            this.samples = this.samples.filter(it => it.timestamp >= Date.now() - (this.viewDurationSeconds + 5) * 1000);
            this.postState();
        });
    }
    private postState() {
        if (this.webviewView?.visible)
            this.webviewView.webview.postMessage({ samples: this.samples, frozen: this.countersProcess === undefined });
    }
}

type UsageSample = { timestamp: number } & Partial<Record<'cpuUsage' | 'workingSet' | 'gcHeapSize' | 'timeInGC', number>>;
