import { StatusBarController } from '../controllers/statusbarController';
import { ProcessArgumentBuilder } from '../interop/processArgumentBuilder';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

export class DotNetTaskProvider implements vscode.TaskProvider {
    resolveTask(task: vscode.Task, token: vscode.CancellationToken): vscode.ProviderResult<vscode.Task> { 
        if (StatusBarController.project == undefined || StatusBarController.configuration === undefined)
            return undefined;
        
        return DotNetTaskProvider.getTask(task.definition, StatusBarController.project.path, 'build', StatusBarController.configuration, StatusBarController.framework);
    }
    provideTasks(token: vscode.CancellationToken): vscode.ProviderResult<vscode.Task[]> {
        if (StatusBarController.project == undefined || StatusBarController.configuration === undefined)
            return undefined;
        
        return [ 
            DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, StatusBarController.project.path, 'build', StatusBarController.configuration, StatusBarController.framework),
        ];
    }

    public static getTestTask(projectFile: string, configuration: string | undefined): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, projectFile, 'test');
    }
    public static getBuildTask(projectFile: string, configuration: string | undefined): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, projectFile, 'build', configuration);
    }
    public static getRestoreTask(projectFile: string): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, projectFile, 'restore');
    }
    public static getCleanTask(projectFile: string): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, projectFile, 'clean');
    }

    private static getTask(definition: vscode.TaskDefinition, projectPath: string, target: string, configuration: string | undefined = undefined, framework: string | undefined = undefined): vscode.Task {
        const builder = new ProcessArgumentBuilder('dotnet')
            .append(target).append(projectPath)
            .conditional(`-p:Configuration=${configuration}`, () => configuration !== undefined)
            .conditional(`-p:TargetFramework=${framework}`, () => framework !== undefined);

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
