import * as vscode from 'vscode';

export class ExternalTypeResolver {
    public static feature : ExternalTypeResolver = new ExternalTypeResolver();
    public transportId: string | undefined;

    public async activate(context: vscode.ExtensionContext): Promise<void> {
        this.transportId = `dotrush-${process.pid}`;
    }
}