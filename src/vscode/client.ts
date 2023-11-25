import { LanguageClient, ServerOptions } from "vscode-languageclient/node";
import * as res from './resources';
import * as vscode from 'vscode';
import * as path from 'path';


export class ClientController {
    private static client: LanguageClient;

    private static initialize(context: vscode.ExtensionContext) {
        const extensionPath = context.extensionPath;
        const serverExecutable = path.join(extensionPath, "extension", "bin", "DotRush");
        const serverExtension = process.platform === 'win32' ? '.exe' : '';
        const serverOptions: ServerOptions = {
            command: serverExecutable + serverExtension,
            args: [ process.pid.toString() ]
        };

        ClientController.client = new LanguageClient(res.extensionId, res.extensionId, serverOptions, { 
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


    public static async activate(context: vscode.ExtensionContext) {
        if (ClientController.client !== undefined && ClientController.client.isRunning())
            return;

        if (vscode.workspace.workspaceFolders?.length === 1 && vscode.workspace.workspaceFile === undefined) {
            const csprojFiles = await vscode.workspace.findFiles(path.join('**', '*.csproj'));
            if (csprojFiles != undefined && csprojFiles.length > 1) {
                const message = res.messageMultipleProjectFilesFound.replace('{0}', csprojFiles.length.toString());
                const result = await vscode.window.showWarningMessage(message, res.messageLoadAll);
                if (result !== res.messageLoadAll)
                    return;
            }
        }
        
        ClientController.initialize(context);
        ClientController.client.start();

        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async ev => {
            if (!ev.fileName.endsWith('.csproj'))
                return;
            const message = res.messageProjectChanged.replace('{0}', path.basename(ev.fileName));
            const result = await vscode.window.showWarningMessage(message, res.messageReload);
            if (result !== undefined)
                vscode.commands.executeCommand(res.commandIdReloadWindow);
        }));
    }

    public static stop() {
        if (ClientController.client !== undefined && ClientController.client.isRunning())
            ClientController.client.stop();
    }
}