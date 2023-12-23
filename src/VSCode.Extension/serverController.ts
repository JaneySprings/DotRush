import { LanguageClient, ServerOptions } from "vscode-languageclient/node";
import * as res from './resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class ServerController {
    private static client: LanguageClient;

    public static isRunning(): boolean {
        return ServerController.client !== undefined && ServerController.client.isRunning();
    }

    public static async activate(context: vscode.ExtensionContext) {
        if (ServerController.client !== undefined && ServerController.client.isRunning())
            return;
        
        ServerController.initialize(context);
        ServerController.client.start();

        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async ev => {
            if (!ev.fileName.endsWith('.csproj'))
                return;
            const message = res.messageProjectChanged.replace('{0}', path.basename(ev.fileName));
            const result = await vscode.window.showWarningMessage(message, res.messageReload);
            if (result !== undefined)
                vscode.commands.executeCommand(res.commandIdReloadWindow);
        }));
    }
    private static initialize(context: vscode.ExtensionContext) {
        const extensionPath = context.extensionPath;
        const serverExecutable = path.join(extensionPath, "extension", "bin", "DotRush");
        const serverExtension = process.platform === 'win32' ? '.exe' : '';
        const serverOptions: ServerOptions = {
            command: serverExecutable + serverExtension,
            args: [ process.pid.toString() ]
        };

        ServerController.client = new LanguageClient(res.extensionId, res.extensionId, serverOptions, { 
            documentSelector: [{ scheme: "file", language: "csharp" }],
            diagnosticCollectionName: res.microsoftProblemMatcherId,
            progressOnInitialization: true,
            synchronize: { 
                configurationSection: res.extensionId,
            },
            connectionOptions: {
                maxRestartCount: 0,
            }
        });
    }
    public static stop() {
        if (ServerController.client !== undefined && ServerController.client.isRunning())
            ServerController.client.stop();
    }
}
