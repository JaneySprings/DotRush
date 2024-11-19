import { DotNetDebugConfigurationProvider } from '../providers/dotnetDebugConfigurationProvider';
import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { StatusBarController } from './statusbarController';
import { ProcessItem } from '../models/process';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export class DebugAdapterController {
    public static async activate(context: vscode.ExtensionContext) : Promise<void> {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdPickProcess, async () => await DebugAdapterController.pickProcessId()));

        context.subscriptions.push(vscode.tasks.registerTaskProvider(res.taskDefinitionId, new DotNetTaskProvider()));
        context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider(res.debuggerVsdbgId, new DotNetDebugConfigurationProvider()));

        if (!fs.existsSync(path.join(context.extensionPath, 'extension', 'bin', 'Debugger')))
            await DebugAdapterController.installDebugger();
    }

    public static async getProgramPath(): Promise<string | undefined> {
        if (StatusBarController.project === undefined || StatusBarController.configuration === undefined)
            return await DebugAdapterController.pickProgramPath();

        const assemblyPath = Interop.getPropertyValue('TargetPath', StatusBarController.project, StatusBarController.configuration, StatusBarController.framework);
		if (!assemblyPath)
			return await DebugAdapterController.pickProgramPath();

        const programDirectory = path.dirname(assemblyPath);
        const programFile = path.basename(assemblyPath, '.dll');
        const programPath = path.join(programDirectory, programFile + Interop.execExtension);
		return programPath;
	}
    public static async getProcessId(): Promise<string | undefined> {
        return await DebugAdapterController.pickProcessId();
    }
    
    private static async pickProgramPath(): Promise<string | undefined> {
        const programPath = await vscode.window.showOpenDialog({
            title: res.messageSelectProgramTitle,
            canSelectFiles: true,
            canSelectFolders: false,
            canSelectMany: false
        });
        return programPath?.[0].fsPath;
    }
    private static async pickProcessId(): Promise<string | undefined> {
        const processes = await Interop.getProcesses();
        const selectedItem = await vscode.window.showQuickPick(processes.map(p => new ProcessItem(p)), { placeHolder: res.messageSelectProcessTitle });
        return selectedItem?.item.id.toString();
    }
    private static async installDebugger(): Promise<void> {
        const result = await Interop.installDebugger();
        if (!result.isSuccess) {
            const channel = vscode.window.createOutputChannel(res.extensionId);
            channel.appendLine(`Failed to install debugger: ${result.message}`);
            channel.show();
        }
    }
//     private static async findProjectFile(directory: string): Promise<string | undefined> {
//         const projectFiles = await vscode.workspace.findFiles(new vscode.RelativePattern(directory, '*.csproj'));
// 		if (projectFiles.length > 1)
// 			return undefined;
//         if (projectFiles.length === 1)
//             return projectFiles[0].fsPath;
        
//         const parentDirectory = path.dirname(directory);
//         if (parentDirectory === directory)
//             return undefined;

//         return DebugAdapterController.findProjectFile(parentDirectory);
//     }
}