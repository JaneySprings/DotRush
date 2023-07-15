import { LanguageClient, ServerOptions, TransportKind } from "vscode-languageclient/node";
import { extensions } from "vscode";
import * as res from './resources';
import * as vscode from 'vscode';
import * as path from 'path';
import { RuntimeController } from "./selector";


export class ClientController {
    private static client: LanguageClient;

    private static initialize() {
        const extensionPath = extensions.getExtension(`${res.extensionPublisher}.${res.extensionId}`)?.extensionPath ?? '';
        const runtimeDirectory = RuntimeController.targetFolderName;
        const serverExecutable = path.join(extensionPath, "extension", "bin", runtimeDirectory, "DotRush");
        const serverExtension = process.platform === 'win32' ? '.exe' : '';
        const serverOptions: ServerOptions = {
            command: serverExecutable + serverExtension,
            transport: TransportKind.stdio,
            args: [ process.pid.toString() ]
        };

        ClientController.client = new LanguageClient(res.extensionId, res.extensionId, serverOptions, { 
            documentSelector: [{ scheme: "file", language: "csharp" }],
            diagnosticCollectionName: res.extensionId,
            progressOnInitialization: true,
            synchronize: { 
                configurationSection: res.extensionId,
            }
        });
    }


    public static async activate(context: vscode.ExtensionContext) {
        if (ClientController.client !== undefined && ClientController.client.isRunning())
            return;
        ClientController.initialize();
        ClientController.client.start();
    }
    public static stop() {
        if (ClientController.client !== undefined && ClientController.client.isRunning())
            ClientController.client.stop();
    }
}
