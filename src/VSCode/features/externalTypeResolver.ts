import { LanguageServerController } from '../controllers/languageServerController';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

export class ExternalTypeResolver {
    public static feature: ExternalTypeResolver = new ExternalTypeResolver();

    public async activate(context: vscode.ExtensionContext): Promise<void> {
        context.subscriptions.push(vscode.debug.onDidStartDebugSession((session) => {
            if (session.type !== res.debuggerUnityId)
                return;

            const baseImplementation = session.customRequest;
            session.customRequest = async (command: string, args?: any) => {
                if (command !== 'resolveType')
                    return baseImplementation(command, args);

                return await LanguageServerController.sendRequest<any>('dotrush/resolveType', args);
            };
        }));
    }
}