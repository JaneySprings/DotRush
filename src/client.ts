import { LanguageClient, ServerOptions } from "vscode-languageclient/node";
import { extensions } from "vscode";
import * as res from './resources';
import * as vscode from 'vscode';
import * as path from 'path';


export class ClientController {
    private static client: LanguageClient;

    private static initialize() {
        const extensionPath = extensions.getExtension(`${res.extensionPublisher}.${res.extensionId}`)?.extensionPath ?? '';
        let serverExecutable = path.join(extensionPath, "extension", "bin", "DotRush");

        if (process.platform === 'win32')
            serverExecutable += '.exe';
        
        const serverOptions: ServerOptions = { 
            command: serverExecutable, 
            args: [ 
                process.pid.toString() 
            ], 
        };

        ClientController.client = new LanguageClient(res.extensionId, res.extensionId, serverOptions, { 
            documentSelector: [{ scheme: "file", language: "csharp" }],
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
