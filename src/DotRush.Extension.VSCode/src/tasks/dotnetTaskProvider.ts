import { ProcessArgumentBuilder } from '../processes/processArgumentBuilder';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

export class DotNetTaskProvider {
    public static async getBuildTaskAsync(projectFile: vscode.Uri, ...args: string[]): Promise<vscode.Task> {
        return await DotNetTaskProvider.getTaskAsync(projectFile, 'build', ...args);
    }
    public static async getRestoreTaskAsync(projectFile: vscode.Uri, ...args: string[]): Promise<vscode.Task> {
        return await DotNetTaskProvider.getTaskAsync(projectFile, 'restore', ...args);
    }
    public static async getCleanTaskAsync(projectFile: vscode.Uri, ...args: string[]): Promise<vscode.Task> {
        return await DotNetTaskProvider.getTaskAsync(projectFile, 'clean', ...args);
    }

    private static async getTaskAsync(projectFile: vscode.Uri, target: string, ...args: string[]): Promise<vscode.Task> {
        const builder = new ProcessArgumentBuilder('dotnet')
            .append(target)
            .appendQuoted(projectFile.fsPath)
            .append(...args);

        return new vscode.Task({ type: `${res.extensionId}.task` },
            vscode.TaskScope.Workspace, target, res.extensionId, new vscode.ShellExecution(builder.build()), res.microsoftProblemMatcherId
        );
    }
}
