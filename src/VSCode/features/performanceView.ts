import { ProcessArgumentBuilder } from '../interop/processArgumentBuilder';
import { Extensions } from '../extensions';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

enum DotNetProfilerType {
    Trace = 'trace',
    Dump = 'gcdump',
    // sample, SOS, etc.
}

export class PerformanceView implements vscode.DebugAdapterTrackerFactory {
    public static feature: PerformanceView = new PerformanceView();

    private processId: number | undefined;

    public activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.debug.registerDebugAdapterTrackerFactory(res.debuggerNetCoreId, this));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdAttachTraceProfiler, async () => await PerformanceView.feature.startProfiler(this.processId, DotNetProfilerType.Trace)));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdCreateHeapDump, async () => await PerformanceView.feature.startProfiler(this.processId, DotNetProfilerType.Dump)));
        context.subscriptions.push(vscode.debug.onDidStartDebugSession(s => {
            if (s.type === res.debuggerNetCoreId && s.configuration.processId !== undefined)
                this.processId = s.configuration.processId;
        }));
    }

    public createDebugAdapterTracker(session: vscode.DebugSession): vscode.ProviderResult<vscode.DebugAdapterTracker> {
        const performanceView = this;
        return {
            onDidSendMessage(message: any) {
                if (message.type != 'event' || message.event != 'process')
                    return;

                performanceView.processId = message.body.systemProcessId;
            },
            onWillStopSession() {
                performanceView.processId = undefined;
            }
        }
    }

    private async startProfiler(processId: number | undefined, profilerType: DotNetProfilerType): Promise<vscode.TaskExecution | undefined> {
        if (processId === undefined)
            processId = await vscode.commands.executeCommand(res.commandIdPickProcess);
        if (processId === undefined)
            return undefined;

        const task = PerformanceView.feature.getProfilerTask(processId, profilerType);
        return vscode.tasks.executeTask(task);
    }
    private getProfilerTask(processId: number, profilerType: DotNetProfilerType): vscode.Task {
        const options: vscode.ShellExecutionOptions = { cwd: Extensions.getCurrentWorkingDirectory() };
        const toolPath = path.join(Interop.binariesPath, 'Diagnostics', `dotnet-${profilerType}${Interop.execExtension}`);
        const builder = new ProcessArgumentBuilder(toolPath)
            .append('collect')
            .append('-p').append(processId.toString());

        if (profilerType === DotNetProfilerType.Trace) {
            builder.append('--format').append('speedscope');
        }
    
        return new vscode.Task(
            { type: res.taskDefinitionId }, 
            vscode.TaskScope.Workspace, 
            'Profile',
            res.extensionId,
            new vscode.ShellExecution(builder.getCommand(), builder.getArguments(), options),
        );
    }
}
