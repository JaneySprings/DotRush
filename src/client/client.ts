import { LanguageClient } from "vscode-languageclient/node";
import { waitForActivation } from './integration';
import { extensions } from "vscode";
import * as vscode from 'vscode';
import * as res from './resources';
import * as path from 'path';


export class ClientController {
    private static extensionPath: string = extensions.getExtension(`${res.extensionPublisher}.${res.extensionId}`)?.extensionPath ?? '';
    private static serverExecutable: string = path.join(ClientController.extensionPath, "extension", "bin", "dotRush"); 
    private static client: LanguageClient;
    private static frameworkList: string[] | undefined;
    private static devicePlatform: string | undefined;

    private static initialize(target: string | undefined) {
        const launchArguments = [ process.pid.toString(), target ?? '-' ];
        let serverExecutable = ClientController.serverExecutable;

        for (const folder of vscode.workspace.workspaceFolders ?? [])
            launchArguments.push(folder.uri.fsPath);

        if (process.platform === 'win32')
            serverExecutable += '.exe';

        ClientController.client = new LanguageClient(
            res.extensionId, res.extensionId, 
            { command: serverExecutable, args: launchArguments, }, 
            { documentSelector: [{ scheme: "file", language: "csharp" }]}
        );
    }


    public static start(target: string | undefined = undefined) {
        ClientController.initialize(target);
        ClientController.client.start();
    }
    public static stop() {
        if (ClientController.client !== undefined)
            ClientController.client.stop();
    }
    public static restart(target: string | undefined = undefined) {
        if (ClientController.client !== undefined)
            ClientController.client.stop();
        ClientController.start(target);
    }


    public static async activate(context: vscode.ExtensionContext) {
        const extensionContext = await waitForActivation(res.extensionMeteorId);
        if (extensionContext === undefined) {
            ClientController.restart();
            return;
        }
    
        extensionContext?.exports.deviceChangedEventHandler.add((device: any) => {
            ClientController.devicePlatform = device?.platform;
            ClientController.restart(ClientController.frameworkList?.find(f => {
                return f.includes(ClientController.devicePlatform ?? 'undefined')
            }));
        });
        extensionContext?.exports.projectChangedEventHandler.add((project: any) => {
            ClientController.frameworkList = project?.frameworks;
        });
    }
}
