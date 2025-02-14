import { DotNetDebugConfigurationProvider } from '../providers/dotnetDebugConfigurationProvider';
import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { StatusBarController } from './statusbarController';
import { ProcessItem } from '../models/process';
import { Extensions } from '../extensions';
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

        if (!fs.existsSync(path.join(context.extensionPath, 'extension', 'bin', 'Debugger')))
            await DebugAdapterController.installDebugger(context);
    }

    public static provideDebuggerOptions(options: vscode.DebugConfiguration): vscode.DebugConfiguration {
        if (options.justMyCode === undefined)
            options.justMyCode = Extensions.getSetting('debugger.projectAssembliesOnly', false);
        if (options.enableStepFiltering === undefined)
            options.enableStepFiltering = Extensions.getSetting('debugger.stepOverPropertiesAndOperators', false);
        if (options.console === undefined)
            options.console = Extensions.getSetting('debugger.console');
        if (options.symbolOptions === undefined)
            options.symbolOptions = {
                searchPaths: Extensions.getSetting('debugger.symbolSearchPaths'),
                searchMicrosoftSymbolServer: Extensions.getSetting('debugger.searchMicrosoftSymbolServer', false),
            };
        if (options.sourceLinkOptions === undefined)
            options.sourceLinkOptions = {
                "*": { enabled: Extensions.getSetting('debugger.automaticSourcelinkDownload', true) }
            }

        return options;
    }
    public static async getProgramPath(): Promise<string | undefined> {
        if (StatusBarController.activeProject === undefined || StatusBarController.activeConfiguration === undefined)
            return await DebugAdapterController.showQuickPickProgram();

        const assemblyPath = Interop.getPropertyValue('TargetPath', StatusBarController.activeProject.path, StatusBarController.activeConfiguration, StatusBarController.activeFramework);
		if (!assemblyPath)
			return await DebugAdapterController.showQuickPickProgram();

        const programDirectory = path.dirname(assemblyPath);
        const programFile = path.basename(assemblyPath, '.dll');
        const programPath = path.join(programDirectory, programFile + Interop.execExtension);
		return programPath;
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
    private static async installDebugger(context: vscode.ExtensionContext): Promise<void> {
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