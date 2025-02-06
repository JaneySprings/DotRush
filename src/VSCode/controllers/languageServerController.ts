import { LanguageClient, ServerOptions } from "vscode-languageclient/node";
import { Extensions } from "../extensions";
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class LanguageServerController {
    private static client: LanguageClient;
    private static command: string;
    private static running: boolean;

    public static async activate(context: vscode.ExtensionContext): Promise<void> {
        const serverExecutable = path.join(context.extensionPath, "extension", "bin", "LanguageServer", "DotRush");
        const serverExtension = process.platform === 'win32' ? '.exe' : '';
        LanguageServerController.command = serverExecutable + serverExtension;
        
        if (await LanguageServerController.shouldQuickPickTargets())
            await LanguageServerController.showQuickPickTargets();

        LanguageServerController.start();

        context.subscriptions.push(LanguageServerController.client);
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdRestartServer, () => LanguageServerController.restart()));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdPickTargets, () => LanguageServerController.showQuickPickTargets()))
        context.subscriptions.push(vscode.workspace.onDidChangeWorkspaceFolders(() => LanguageServerController.restart()));
        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async e => {
            if (path.extname(e.fileName) !== '.csproj')
                return;

            const value = await vscode.window.showWarningMessage(res.messageProjectChanged, res.messageReload)
            if (value === res.messageReload)
                LanguageServerController.restart();
        }));
    }

    private static initialize() {
        const serverOptions: ServerOptions = { 
            command: LanguageServerController.command,
            options: { cwd: Extensions.getCurrentWorkingDirectory() }
        };
        LanguageServerController.client = new LanguageClient(res.extensionId, res.extensionId, serverOptions, { 
            documentSelector: [
                { pattern: '**/*.cs' },
                { pattern: '**/*.xaml' }
            ],
            progressOnInitialization: true, 
            synchronize: { 
                configurationSection: res.extensionId,
                fileEvents: vscode.workspace.createFileSystemWatcher('**/*.*'),
            },
            connectionOptions: {
                maxRestartCount: 2,
            }
        });
    }
    public static start() {
        LanguageServerController.initialize();
        LanguageServerController.client.start();
        LanguageServerController.running = true;
    }
    public static stop() {
        LanguageServerController.client.stop();
        LanguageServerController.client.dispose();
        LanguageServerController.running = false;
    }
    public static restart() {
        LanguageServerController.stop();
        LanguageServerController.start();
    }
    public static isRunning(): boolean {
        return LanguageServerController.running;
    }

    private static async showQuickPickTargets(): Promise<void> {
        const result = await Extensions.selectProjectOrSolutionFiles();
        await Extensions.putSetting(res.configIdRoslynProjectOrSolutionFiles, result, vscode.ConfigurationTarget.Workspace);
        if (LanguageServerController.isRunning())
            LanguageServerController.restart();
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
