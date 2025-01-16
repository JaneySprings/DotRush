import { StatusBarController } from '../controllers/statusbarController';
import { ProcessArgumentBuilder } from '../interop/processArgumentBuilder';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

export class DotNetTaskProvider implements vscode.TaskProvider {
    resolveTask(task: vscode.Task, token: vscode.CancellationToken): vscode.ProviderResult<vscode.Task> { 
        if (StatusBarController.activeProject == undefined || StatusBarController.activeConfiguration === undefined)
            return undefined;
        
        return DotNetTaskProvider.getTask(task.definition, StatusBarController.activeProject.path, 'build', StatusBarController.activeConfiguration, StatusBarController.activeFramework);
    }
    provideTasks(token: vscode.CancellationToken): vscode.ProviderResult<vscode.Task[]> {
        if (StatusBarController.activeProject == undefined || StatusBarController.activeConfiguration === undefined)
            return undefined;
        
        return [ 
            DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, StatusBarController.activeProject.path, 'build', StatusBarController.activeConfiguration, StatusBarController.activeFramework),
        ];
    }

    public static getTestTask(projectFile: string, args: string[] | undefined = undefined): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId, args: args }, projectFile, 'test', StatusBarController.activeConfiguration);
    }
    public static getBuildTask(projectFile: string): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId }, projectFile, 'build', StatusBarController.activeConfiguration);
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
