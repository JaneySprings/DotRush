import { StatusBarController } from '../controllers/statusbarController';
import { ProcessArgumentBuilder } from '../interop/processArgumentBuilder';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

export class DotNetTaskProvider implements vscode.TaskProvider {
    resolveTask(task: vscode.Task, token: vscode.CancellationToken): vscode.ProviderResult<vscode.Task> { 
        if (StatusBarController.project == undefined || StatusBarController.configuration === undefined)
            return undefined;
        
        return DotNetTaskProvider.getTask(task.definition, StatusBarController.project.path, 'build');
    }
    provideTasks(token: vscode.CancellationToken): vscode.ProviderResult<vscode.Task[]> {
        if (StatusBarController.project == undefined || StatusBarController.configuration === undefined)
            return undefined;
        
        return [ 
            DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, StatusBarController.project.path, 'build')
        ];
    }

    public static getTestTask(projectFile: string): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, projectFile, 'test');
    }
    public static getBuildTask(projectFile: string): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, projectFile, 'build');
    }
    public static getRestoreTask(projectFile: string): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, projectFile, 'restore');
    }
    public static getCleanTask(projectFile: string): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, projectFile, 'clean');
    }

    private static getTask(definition: vscode.TaskDefinition, projectPath: string, target: string): vscode.Task {
        const builder = new ProcessArgumentBuilder('dotnet')
            .append(target).append(projectPath)
            .append(`-p:Configuration=${StatusBarController.configuration}`);

        definition.args?.forEach((arg: string) => builder.override(arg));
        
        return new vscode.Task(
            definition, 
            vscode.TaskScope.Workspace, 
            'Build',
            res.extensionId,
            new vscode.ShellExecution(builder.getCommand(), builder.getArguments()),
            `$${res.microsoftProblemMatcherId}`
        );
    }
}
