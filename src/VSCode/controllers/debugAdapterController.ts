import { DotNetDebugConfigurationProvider } from '../providers/dotnetDebugConfigurationProvider';
import { MonoDebugConfigurationProvider } from '../providers/monoDebugConfigurationProvider';
import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { ProcessItem } from '../models/process';
import { Interop } from '../interop/interop';
import { Extensions } from '../extensions';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export class DebugAdapterController {
    public static async activate(context: vscode.ExtensionContext) : Promise<void> {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdPickProcess, async () => await DebugAdapterController.showQuickPickProcess()));

        context.subscriptions.push(vscode.tasks.registerTaskProvider(res.taskDefinitionId, new DotNetTaskProvider()));
        context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider(res.debuggerVsdbgId, new DotNetDebugConfigurationProvider()));
        context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider(res.debuggerUnityId, new MonoDebugConfigurationProvider()));

        if (!fs.existsSync(path.join(context.extensionPath, 'extension', 'bin', 'Debugger')))
            await DebugAdapterController.installDebugger(Extensions.onVSCode(res.debuggerVsdbgInstallId, res.debuggerNcdbgInstallId));
    }

    public static async showQuickPickProgram(): Promise<string | undefined> {
        const programPath = await vscode.window.showOpenDialog({
            title: res.messageSelectProgramTitle,
            canSelectFiles: true,
            canSelectFolders: false,
            canSelectMany: false
        });
        return programPath?.[0].fsPath;
    }
    public static async showQuickPickProcess(): Promise<string | undefined> {
        const processes = await Interop.getProcesses();
        const selectedItem = await vscode.window.showQuickPick(processes.map(p => new ProcessItem(p)), { placeHolder: res.messageSelectProcessTitle });
        return selectedItem?.item.id.toString();
    }
    
    private static async installDebugger(id: string): Promise<void> {
        const getNameByDebuggerId = (id: string) => {
            switch (id) {
                case res.debuggerVsdbgInstallId: return res.debuggerVsdbgDisplayName;
                case res.debuggerNcdbgInstallId: return res.debuggerNcdbgDisplayName;
                default: return id;
            }
        };
        const options : vscode.ProgressOptions = {
            title: res.messageInstallingComponentTitle + getNameByDebuggerId(id),
            location: vscode.ProgressLocation.Notification,
            cancellable: false
        };
        await vscode.window.withProgress(options, (_p, _ct) => Interop.installDebugger(id));
    }
}