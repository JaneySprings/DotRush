import { LanguageClient, ServerOptions } from "vscode-languageclient/node";
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class LanguageServerController {
    private static client: LanguageClient;
    private static command: string;

    public static activate(context: vscode.ExtensionContext) {
        const extensionPath = context.extensionPath;
        const serverExecutable = path.join(extensionPath, "extension", "bin", "LanguageServer", "DotRush");
        const serverExtension = process.platform === 'win32' ? '.exe' : '';
        LanguageServerController.command = serverExecutable + serverExtension;
        LanguageServerController.start();

        context.subscriptions.push(LanguageServerController.client);
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdRestartServer, () => LanguageServerController.restart()));
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
        const serverOptions: ServerOptions = { command: LanguageServerController.command };
        LanguageServerController.client = new LanguageClient(res.extensionId, res.extensionId, serverOptions, { 
            diagnosticCollectionName: res.microsoftProblemMatcherId,
            progressOnInitialization: true,
            synchronize: { 
                configurationSection: res.extensionId,
            },
            connectionOptions: {
                maxRestartCount: 2,
            }
        });
    }
    public static start() {
        LanguageServerController.initialize();
        LanguageServerController.client.start();
    }
    public static stop() {
        LanguageServerController.client.stop();
        LanguageServerController.client.dispose();
    }
    public static restart() {
        LanguageServerController.stop();
        LanguageServerController.start();
    }
}
