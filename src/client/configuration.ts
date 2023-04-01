import * as vscode from 'vscode';
import * as res from './resources';

export class Configuration {
    public static getSetting<T>(option: string): T | undefined { 
        const config = vscode.workspace.getConfiguration(res.extensionId);
        return config.get<T>(option);
    }
}