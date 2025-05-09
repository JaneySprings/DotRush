import { LanguageClient, LanguageClientOptions, ServerOptions } from "vscode-languageclient/node";
import { TestExplorerController } from "./testExplorerController";
import { PublicExports } from "../publicExports";
import { Extensions } from "../extensions";
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class LanguageServerController {
    private static client: LanguageClient;
    private static serverOptions: ServerOptions;
    private static clientOptions: LanguageClientOptions;
    private static running: boolean;

    public static async activate(context: vscode.ExtensionContext): Promise<void> {
        const serverExecutable = path.join(context.extensionPath, "extension", "bin", "LanguageServer", "DotRush");
        const serverExtension = process.platform === 'win32' ? '.exe' : '';
        LanguageServerController.serverOptions = {
            command: serverExecutable + serverExtension,
            options: { cwd: Extensions.getCurrentWorkingDirectory() }
        };
        LanguageServerController.clientOptions = {
            documentSelector: [
                { pattern: '**/*.cs' },
                { pattern: '**/*.xaml' }
            ],
            diagnosticCollectionName: res.extensionId,
            progressOnInitialization: true,
            synchronize: {
                configurationSection: res.extensionId
            },
            connectionOptions: {
                maxRestartCount: 2,
            }
        };

        if (await LanguageServerController.shouldQuickPickTargets())
            await LanguageServerController.showQuickPickTargets();

        LanguageServerController.start();

        context.subscriptions.push(LanguageServerController.client);
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdReloadWorkspace, () => LanguageServerController.reload()));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdPickTargets, () => LanguageServerController.showQuickPickTargets()))
        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async e => {
            const extName = path.extname(e.fileName);
            if (extName !== '.csproj' && extName !== '.props')
                return;

            const value = await vscode.window.showWarningMessage(res.messageProjectChanged, res.messageReload)
            if (value === res.messageReload)
                LanguageServerController.reload();
        }));
    }

    private static initialize() {
        LanguageServerController.client = new LanguageClient(res.extensionId, res.extensionId, LanguageServerController.serverOptions, LanguageServerController.clientOptions);
        LanguageServerController.client.onNotification('dotrush/projectLoaded', (project: string) => {
            TestExplorerController.loadProject(project);
            PublicExports.instance.onProjectLoaded.invoke(project);
        });
    }
    public static start() {
        LanguageServerController.initialize();
        LanguageServerController.client.start();
        LanguageServerController.running = true;
    }
    public static stop() {
        LanguageServerController.client.stop();
        LanguageServerController.running = false;
        TestExplorerController.unloadProjects();
    }
    public static reload() {
        if (!LanguageServerController.running)
            return;

        const workspaceFolders = vscode.workspace.workspaceFolders?.map(folder => ({ uri: folder.uri.toString(), name: folder.name }));
        LanguageServerController.client.sendNotification('dotrush/reloadWorkspace', {
            workspaceFolders: workspaceFolders,
        });
    }
    public static isRunning(): boolean {
        return LanguageServerController.running;
    }

    private static async showQuickPickTargets(): Promise<void> {
        const result = await Extensions.selectProjectOrSolutionFiles();
        if (result === undefined)
            return;

        await Extensions.putSetting(res.configIdRoslynProjectOrSolutionFiles, result, vscode.ConfigurationTarget.Workspace);
        if (LanguageServerController.isRunning())
            LanguageServerController.reload();
    }
    private static async shouldQuickPickTargets(): Promise<boolean> {
        const projectOrSolutionFiles = Extensions.getSetting<string[]>(res.configIdRoslynProjectOrSolutionFiles);
        if (projectOrSolutionFiles !== undefined && projectOrSolutionFiles.length > 0)
            return false;

        const solutions = await Extensions.getSolutionFiles();
        const projects = await Extensions.getProjectFiles();
        if (solutions.length === 1 || projects.length === 1)
            return false;

        return solutions.length > 1 || projects.length > 1;
    }
}
