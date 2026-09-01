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
    public static tracker: DebugAdapterTracker;

    public static activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdPickProcess, async () => await DebugAdapterController.showQuickPickProcess()));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdActiveTargetPath, async () => await DebugAdapterController.getProjectTargetPath(
            StatusBarController.activeProject?.path,
            StatusBarController.activeConfiguration,
            StatusBarController.activeFramework
        )));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdActiveTargetBinaryPath, async () => await DebugAdapterController.getProjectTargetBinaryPath(
            StatusBarController.activeProject?.path,
            StatusBarController.activeConfiguration,
            StatusBarController.activeFramework
        )));
        context.subscriptions.push(vscode.tasks.registerTaskProvider(res.taskDefinitionId, new DotNetTaskProvider()));
        context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider(res.debuggerNetCoreId, new DotNetDebugConfigurationProvider()));
        context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider(res.debuggerUnityId, new MonoDebugConfigurationProvider()));

        DebugAdapterController.tracker = new DebugAdapterTracker();
        context.subscriptions.push(vscode.debug.registerDebugAdapterTrackerFactory(res.debuggerNetCoreId, DebugAdapterController.tracker));
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
    public static async getProjectTargetPath(projectPath?: string, configuration?: string, framework?: string): Promise<string | undefined> {
        if (projectPath === undefined)
            projectPath = StatusBarController.activeProject?.path;
        if (configuration === undefined)
            configuration = StatusBarController.activeConfiguration;
        if (projectPath === undefined)
            return await DebugAdapterController.showQuickPickProgram();

        const targetPath = Interop.getPropertyValue('TargetPath', projectPath, configuration, framework);
        if (!targetPath)
            return await DebugAdapterController.showQuickPickProgram();

        return targetPath;
    }
    public static async getProjectTargetBinaryPath(projectPath?: string, configuration?: string, framework?: string): Promise<string | undefined> {
        const assemblyPath = await DebugAdapterController.getProjectTargetPath(projectPath, configuration, framework);
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

class DebugAdapterTracker implements vscode.DebugAdapterTrackerFactory {
    private readonly onModuleLoadedEmitter = new vscode.EventEmitter<any>();
    private readonly onProcessStartedEmitter = new vscode.EventEmitter<number>();
    private readonly onTargetExitedEmitter = new vscode.EventEmitter<void>();

    public readonly onModuleLoaded: vscode.Event<any> = this.onModuleLoadedEmitter.event;
    public readonly onProcessStarted: vscode.Event<number> = this.onProcessStartedEmitter.event;
    public readonly onTargetExited: vscode.Event<void> = this.onTargetExitedEmitter.event;

    createDebugAdapterTracker(session: vscode.DebugSession): vscode.ProviderResult<vscode.DebugAdapterTracker> {
        const tracker = this;
        return {
            onDidSendMessage(message: any) {
                if (message.type === 'event' && message.event === 'module' && message.body.reason === 'new') {
                    tracker.onModuleLoadedEmitter.fire(message.body.module);
                    return;
                }
                // TODO: add custom same event for attach
                if (message.type === 'event' && message.event === 'process') {
                    tracker.onProcessStartedEmitter.fire(message.body.systemProcessId)
                    return;
                }
            },
            onWillReceiveMessage(message: any) {
                if (message.type === 'request' && message.command === 'attach') {
                    tracker.onProcessStartedEmitter.fire(message.arguments.processId);
                    return;
                }
            },
            onWillStopSession() {
                tracker.onTargetExitedEmitter.fire();
            },
        }
    }
}