import { ProcessArgumentBuilder } from '../processes/processArgumentBuilder';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

export class DotNetTaskProvider {
    public static async getBuildTask(projectFile: vscode.Uri, ...args: string[]): Promise<vscode.Task> {
        return await DotNetTaskProvider.getTask(projectFile, 'build', ...args);
    }
    public static async getRestoreTask(projectFile: vscode.Uri, ...args: string[]): Promise<vscode.Task> {
        return await DotNetTaskProvider.getTask(projectFile, 'restore', ...args);
    }
    public static async getCleanTask(projectFile: vscode.Uri, ...args: string[]): Promise<vscode.Task> {
        return await DotNetTaskProvider.getTask(projectFile, 'clean', ...args);
    }

    private static async getTask(projectFile: vscode.Uri, target: string, ...args: string[]): Promise<vscode.Task> {
        const builder = new ProcessArgumentBuilder('dotnet')
            .append(target)
            .appendQuoted(projectFile.fsPath)
            .append(...args);

        return new vscode.Task({ type: `${res.extensionId}.task` },
            vscode.TaskScope.Workspace, target, res.extensionId, new vscode.ShellExecution(builder.build()), res.microsoftProblemMatcherId
        );
    }
}
