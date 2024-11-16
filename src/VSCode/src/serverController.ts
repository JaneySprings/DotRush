import { LanguageClient, ServerOptions } from "vscode-languageclient/node";
import * as res from './constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class ServerController {
    private static client: LanguageClient;
    private static command: string;

    public static activate(context: vscode.ExtensionContext) {
        const extensionPath = context.extensionPath;
        const serverExecutable = path.join(extensionPath, "extension", "bin", "LanguageServer", "DotRush");
        const serverExtension = process.platform === 'win32' ? '.exe' : '';
        ServerController.command = serverExecutable + serverExtension;
        ServerController.start();

        context.subscriptions.push(ServerController.client);
    }

    private static initialize() {
        const serverOptions: ServerOptions = { command: ServerController.command };
        ServerController.client = new LanguageClient(res.extensionId, res.extensionId, serverOptions, { 
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
        ServerController.initialize();
        ServerController.client.start();
    }
    public static stop() {
        ServerController.client.stop();
        ServerController.client.dispose();
    }
    public static restart() {
        ServerController.stop();
        ServerController.start();
    }
}
