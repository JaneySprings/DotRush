import * as res from './../resources';
import * as vscode from 'vscode';


export class DotNetTaskProvider {
    public static targetFramework: string | undefined;
    public static getTask(target: string, directory: string, useFramework: boolean = false): vscode.Task | undefined { 
        let command = `dotnet ${target} ${directory}`;

        if (DotNetTaskProvider.targetFramework !== undefined && useFramework)
            command += ` -f:${this.targetFramework}`;

        if (target === 'build')
            command += ' -p:EmbedAssembliesIntoApk=true';

        return new vscode.Task({ type: `${res.extensionId}.${res.taskDefinitionId}` }, 
            vscode.TaskScope.Workspace, target, res.extensionId, new vscode.ShellExecution(command)
        );
    }
}