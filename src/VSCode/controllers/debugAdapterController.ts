import { DotNetDebugConfigurationProvider } from '../providers/dotnetDebugConfigurationProvider';
import { MonoDebugConfigurationProvider } from '../providers/monoDebugConfigurationProvider';
import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { ProcessItem } from '../models/process';
import { Interop } from '../interop/interop';
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
            await DebugAdapterController.installNetCoreDebugger(context);
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
    
    private static async installNetCoreDebugger(context: vscode.ExtensionContext): Promise<void> {
        const channel = vscode.window.createOutputChannel(res.extensionId);
        context.subscriptions.push(channel);

        channel.appendLine('Installing VSDBG debugger. This may take a few minutes...');
        const result = await Interop.installDebugger();
        if (!result.isSuccess)
            channel.appendLine(`Failed to install debugger: ${result.message}`);
        else
            channel.appendLine('Debugger installed successfully.');
    }
}