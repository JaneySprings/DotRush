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

    public static provideDebuggerOptions(options: vscode.DebugConfiguration): vscode.DebugConfiguration {
        if (options.justMyCode === undefined)
            options.justMyCode = DebugAdapterController.getSetting('debugger.projectAssembliesOnly', false);
        if (options.enableStepFiltering === undefined)
            options.enableStepFiltering = DebugAdapterController.getSetting('debugger.stepOverPropertiesAndOperators', false);
        if (options.console === undefined)
            options.console = DebugAdapterController.getSetting('debugger.console');
        if (options.symbolOptions === undefined)
            options.symbolOptions = {
                searchPaths: DebugAdapterController.getSetting('debugger.symbolSearchPaths'),
                searchMicrosoftSymbolServer: DebugAdapterController.getSetting('debugger.searchMicrosoftSymbolServer', false),
            };
        if (options.sourceLinkOptions === undefined)
            options.sourceLinkOptions = {
                "*": { enabled: DebugAdapterController.getSetting('debugger.automaticSourcelinkDownload', true) }
            }

        return options;
    }
    public static async getProgramPath(): Promise<string | undefined> {
        if (StatusBarController.activeProject === undefined || StatusBarController.activeConfiguration === undefined)
            return await DebugAdapterController.pickProgramPath();

        const assemblyPath = Interop.getPropertyValue('TargetPath', StatusBarController.activeProject, StatusBarController.activeConfiguration, StatusBarController.activeFramework);
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
        const channel = vscode.window.createOutputChannel(res.extensionId);
        channel.appendLine('Installing VSDBG debugger. This may take a few minutes...');
        channel.show();
        const result = await Interop.installDebugger();
        if (!result.isSuccess)
            channel.appendLine(`Failed to install debugger: ${result.message}`);
        else
            channel.appendLine('Debugger installed successfully.');
    }

    public static getSetting(id: string, fallback: any = undefined): any {
        return vscode.workspace.getConfiguration(res.extensionId).get(id) ?? fallback;
    }
}