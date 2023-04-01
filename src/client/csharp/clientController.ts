import { LanguageClient, LanguageClientOptions, ServerOptions } from "vscode-languageclient/node";
import { extensions } from "vscode";
import * as res from '../resources';
import * as path from 'path';

export class ClientController {
    private static extensionPath: string = extensions.getExtension(`${res.extensionPublisher}.${res.extensionId}`)?.extensionPath ?? '';
    private static serverExecutable: string = path.join(ClientController.extensionPath, "extension", "bin", "dotRush"); 
    private static client: LanguageClient;

    public static currentTarget: string;
    public static currentTargetDirectory: string;

    private static initialize(target: string) {
        const launchArguments = [target, process.pid.toString()];
        let serverExecutable = ClientController.serverExecutable;

        if (process.platform === 'win32')
            serverExecutable += '.exe';

        const serverOptions: ServerOptions = {
            command: serverExecutable,
            args: launchArguments,
        };
        
        const clientOptions: LanguageClientOptions = {
            documentSelector: [{ scheme: "file", language: "csharp" }]
        };
        ClientController.currentTarget = target;
        ClientController.currentTargetDirectory = path.dirname(target);
        ClientController.client = new LanguageClient(res.extensionId, res.extensionId, serverOptions, clientOptions);
    }


    public static start(target: string) {
        ClientController.initialize(target);
        ClientController.client.start();
    }
    public static stop() {
        if (ClientController.client !== undefined)
            ClientController.client.stop();
    }
    public static restart(target: string) {
        if (ClientController.client !== undefined)
            ClientController.client.stop();
        ClientController.start(target);
    }
}
