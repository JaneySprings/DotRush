import { DotNetDebugConfigurationProvider } from '../providers/dotnetDebugConfigurationProvider';
import { MonoDebugConfigurationProvider } from '../providers/monoDebugConfigurationProvider';
import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { StatusBarController } from './statusbarController';
import { LaunchProfile, LaunchSettings } from '../models/profile';
import { ProcessItem } from '../models/process';
import { Interop } from '../interop/interop';
import { Extensions } from '../extensions';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export class DebugAdapterController {
    public static async activate(context: vscode.ExtensionContext): Promise<void> {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdPickProcess, async () => await DebugAdapterController.showQuickPickProcess()));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdActiveTargetPath, async () => await DebugAdapterController.getProjectTargetPath()));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdActiveTargetBinaryPath, async () => await DebugAdapterController.getProjectTargetBinaryPath()));

        context.subscriptions.push(vscode.tasks.registerTaskProvider(res.taskDefinitionId, new DotNetTaskProvider()));
        context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider(res.debuggerNetCoreId, new DotNetDebugConfigurationProvider()));
        context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider(res.debuggerUnityId, new MonoDebugConfigurationProvider()));

        if (!fs.existsSync(path.join(context.extensionPath, 'extension', 'bin', 'Debugger')))
            await DebugAdapterController.installDebugger(Extensions.onVSCode(res.debuggerVsdbgInstallId, res.debuggerNcdbgInstallId));
    }

    public static getLaunchProfile(launchSettingsPath: string, profileName: string | undefined): LaunchProfile | undefined {
        if (!fs.existsSync(launchSettingsPath))
            return undefined;

        const settings = Extensions.deserialize<LaunchSettings>(fs.readFileSync(launchSettingsPath, 'utf-8').trim());
        if (settings?.profiles === undefined || Object.keys(settings.profiles).length === 0)
            return undefined;

        if (profileName !== undefined)
            return settings.profiles[profileName];

        if (settings.profiles['https'] !== undefined) // For web projects, the default profile is 'https'
            return settings.profiles['https'];

        return settings.profiles[Object.keys(settings.profiles)[0]];
    }
    public static getLaunchSettingsPath(): string | undefined {
        const projectPath = StatusBarController.activeProject?.path;
        if (projectPath === undefined)
            return undefined;

        const settingsPath = path.join(path.dirname(projectPath), 'Properties', 'launchSettings.json');
        if (!fs.existsSync(settingsPath))
            return undefined;

        return settingsPath;
    }

    private static async installDebugger(id: string): Promise<void> {
        const getNameByDebuggerId = (id: string) => {
            switch (id) {
                case res.debuggerVsdbgInstallId: return res.debuggerVsdbgDisplayName;
                case res.debuggerNcdbgInstallId: return res.debuggerNcdbgDisplayName;
                default: return id;
            }
        };
        const options: vscode.ProgressOptions = {
            title: res.messageInstallingComponentTitle + getNameByDebuggerId(id),
            location: vscode.ProgressLocation.Notification,
            cancellable: false
        };
        await vscode.window.withProgress(options, (_p, _ct) => Interop.installDebugger(id));
    }
    private static async getProjectTargetPath(): Promise<string | undefined> {
        if (StatusBarController.activeProject === undefined || StatusBarController.activeConfiguration === undefined)
            return await DebugAdapterController.showQuickPickProgram();

        const targetPath = Interop.getPropertyValue('TargetPath', StatusBarController.activeProject.path, StatusBarController.activeConfiguration, StatusBarController.activeFramework);
        if (!targetPath)
            return await DebugAdapterController.showQuickPickProgram();

        return targetPath;
    }
    private static async getProjectTargetBinaryPath(): Promise<string | undefined> {
        const assemblyPath = await DebugAdapterController.getProjectTargetPath();
        if (assemblyPath === undefined)
            return undefined;

        const programDirectory = path.dirname(assemblyPath);
        const programFile = path.basename(assemblyPath, '.dll');
        return path.join(programDirectory, programFile + Interop.execExtension);
    }
    private static async showQuickPickProgram(): Promise<string | undefined> {
        const programPath = await vscode.window.showOpenDialog({
            title: res.messageSelectProgramTitle,
            canSelectFiles: true,
            canSelectFolders: false,
            canSelectMany: false
        });
        return programPath?.[0].fsPath;
    }
    private static async showQuickPickProcess(): Promise<string | undefined> {
        const processes = await Interop.getProcesses();
        if (processes === undefined || processes.length === 0)
            return undefined;

        const selectedItem = await vscode.window.showQuickPick(processes.map(p => new ProcessItem(p)), { placeHolder: res.messageSelectProcessTitle });
        return selectedItem?.item.id.toString();
    }
}