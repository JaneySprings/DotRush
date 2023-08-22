import { LanguageClient, ServerOptions } from "vscode-languageclient/node";
import { ExtensionContext, extensions } from "vscode";
import { RuntimeController } from "./selector";
import * as res from './resources';
import * as path from 'path';


export class ClientController {
    private static client: LanguageClient;

    private static initialize() {
        const extensionPath = extensions.getExtension(`${res.extensionPublisher}.${res.extensionId}`)?.extensionPath ?? '';
        const runtimeDirectory = RuntimeController.targetFolderName;
        const serverExecutable = path.join(extensionPath, "extension", "bin", runtimeDirectory, "DotRush");
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


    public static async activate(context: ExtensionContext) {
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