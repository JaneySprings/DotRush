import * as vscode from 'vscode';

export class ExternalTypeResolver {
    public static feature : ExternalTypeResolver = new ExternalTypeResolver();
    public transportId: string | undefined;

    public async activate(context: vscode.ExtensionContext): Promise<void> {
        if (!context.extension.isActive)
            return;

        this.transportId = `dotrush-${process.pid}`;
    }
}