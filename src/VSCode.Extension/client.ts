import { LanguageClient, LanguageClientOptions } from "vscode-languageclient/node";
import { extensions } from "vscode";
import * as vscode from 'vscode';
import * as res from './resources';
import * as path from 'path';


export class ClientController {
    private static client: LanguageClient;

    private static initialize() {
        const launchArguments = [ process.pid.toString() ];
        const extensionPath = extensions.getExtension(`${res.extensionPublisher}.${res.extensionId}`)?.extensionPath ?? '';
        let serverExecutable = path.join(extensionPath, "extension", "bin", "DotRush");

        for (const folder of vscode.workspace.workspaceFolders ?? [])
            launchArguments.push(folder.uri.fsPath);

        if (process.platform === 'win32')
            serverExecutable += '.exe';

        const clientOptions: LanguageClientOptions = { 
            documentSelector: [{ scheme: "file", language: "csharp" }],
            synchronize: { 
                configurationSection: res.extensionId, 
                fileEvents:  vscode.workspace.createFileSystemWatcher("**/*.cs")
            }
        };

        ClientController.client = new LanguageClient(
            res.extensionId, res.extensionId, 
            { command: serverExecutable, args: launchArguments, }, 
            clientOptions
        );
    }


    public static start() {
        if (ClientController.client !== undefined && ClientController.client.isRunning())
            return;
        ClientController.initialize();
        ClientController.client.start();
    }
    public static stop() {
        if (ClientController.client !== undefined && ClientController.client.isRunning())
            ClientController.client.stop();
    }
    public static restart() {
        ClientController.client.stop();
        ClientController.start();
    }


    public static async activate(context: vscode.ExtensionContext) {
        ClientController.start();
    }

    public static sendReloadTargetsNotification() {
        ClientController.client.diagnostics?.clear();
        ClientController.client.sendNotification('reloadTargets');
    }
}
