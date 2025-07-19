import { StatusBarController } from '../controllers/statusbarController';
import { ProcessArgumentBuilder } from '../interop/processArgumentBuilder';
import { Extensions } from '../extensions';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

enum DotNetTarget {
    Build = 'build',
    Clean = 'clean',
    Restore = 'restore',
    Test = 'test'
}

export class DotNetTaskProvider implements vscode.TaskProvider {
    public static onWindows: boolean = process.platform === 'win32';
    public static onLinux: boolean = process.platform === 'linux';
    public static onMac: boolean = process.platform === 'darwin';

    resolveTask(task: vscode.Task, token: vscode.CancellationToken): vscode.ProviderResult<vscode.Task> { 
        if (StatusBarController.activeProject == undefined || task.definition.type !== res.taskDefinitionId)
            return undefined;
        
        task.definition.target = DotNetTarget.Build;
        if (task.definition.project === undefined)
            task.definition.project = StatusBarController.activeProject.path;

        return DotNetTaskProvider.getTask(task.definition, StatusBarController.activeConfiguration, StatusBarController.activeFramework);
    }
    provideTasks(token: vscode.CancellationToken): vscode.ProviderResult<vscode.Task[]> {
        if (StatusBarController.activeProject == undefined)
            return undefined;
        
        return [ 
            DotNetTaskProvider.getTask({ type: res.taskDefinitionId, target: DotNetTarget.Build, project: StatusBarController.activeProject.path }, StatusBarController.activeConfiguration, StatusBarController.activeFramework),
        ];
    }

    public static getTestTask(projectFile: string, builder: ProcessArgumentBuilder | undefined = undefined): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId, target: DotNetTarget.Test, project: projectFile, args: builder?.getArguments() }, StatusBarController.activeConfiguration);
    }
    public static getBuildTask(projectFile: string): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId, target: DotNetTarget.Build, project: projectFile }, StatusBarController.activeConfiguration);
    }
    public static getRestoreTask(projectFile: string): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId, target: DotNetTarget.Restore, project: projectFile });
    }
    public static getCleanTask(projectFile: string): vscode.Task {
        return DotNetTaskProvider.getTask({ type: res.taskDefinitionId, target: DotNetTarget.Clean, project: projectFile });
    }

    private static getTask(definition: vscode.TaskDefinition, configuration: string | undefined = undefined, framework: string | undefined = undefined): vscode.Task {
        const options: vscode.ShellExecutionOptions = { cwd: Extensions.getCurrentWorkingDirectory() };
        const builder = new ProcessArgumentBuilder('dotnet')
            .append(definition.target).append(definition.project)
            .conditional(`-p:Configuration=${configuration}`, () => configuration !== undefined)
            .conditional(`-p:TargetFramework=${framework}`, () => framework !== undefined);

        if (definition.target === DotNetTarget.Build) {
            builder.conditional('--no-restore', () => Extensions.getSetting<boolean>(res.configIdMSBuildNoRestore, false));
            builder.conditional('--no-dependencies', () => Extensions.getSetting<boolean>(res.configIdMSBuildNoDependencies, false));
            Extensions.getSetting<string[]>(res.configIdMSBuildAdditionalBuildArguments)?.forEach(arg => builder.append(arg));
        }
        if (definition.target === DotNetTarget.Test) {
            Extensions.getSetting<string[]>(res.configIdMSBuildAdditionalTestArguments)?.forEach(arg => builder.append(arg));
        }

        definition.args?.forEach((arg: string) => builder.override(arg));

        return new vscode.Task(
            definition,
            vscode.TaskScope.Workspace,
            Extensions.capitalize(definition.target),
            res.extensionId,
            new vscode.ShellExecution(builder.getCommand(), builder.getArguments(), options)
        );
    }
}
