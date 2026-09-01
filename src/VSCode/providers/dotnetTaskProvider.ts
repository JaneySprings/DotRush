import { StatusBarController } from '../controllers/statusbarController';
import { ProcessArgumentBuilder } from '../interop/processArgumentBuilder';
import { Interop } from '../interop/interop';
import { Extensions } from '../extensions';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

enum DotNetTarget {
    Build = 'build',
    Clean = 'clean',
    Restore = 'restore',
}
enum DotNetProfilerType {
    Trace = 'trace',
    GCDump = 'gcdump',
}

export class DotNetTaskProvider implements vscode.TaskProvider {
    public static onWindows: boolean = process.platform === 'win32';
    public static onLinux: boolean = process.platform === 'linux';
    public static onMac: boolean = process.platform === 'darwin';

    resolveTask(task: vscode.Task, token: vscode.CancellationToken): vscode.ProviderResult<vscode.Task> {
        if (task.definition.type !== res.taskDefinitionId)
            return undefined;

        task.definition.target = DotNetTarget.Build;
        if (task.definition.project === undefined)
            task.definition.project = StatusBarController.activeProject?.path;

        return DotNetTaskProvider.getTask(task.definition, StatusBarController.activeConfiguration, StatusBarController.activeFramework);
    }
    provideTasks(token: vscode.CancellationToken): vscode.ProviderResult<vscode.Task[]> {
        return [
            DotNetTaskProvider.getTask({
                type: res.taskDefinitionId,
                target: DotNetTarget.Build,
                project: `\${command:${res.commandIdActiveProjectPath}}`
            }, StatusBarController.activeConfiguration, StatusBarController.activeFramework)
        ];
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
    public static getTraceTask(processId: number | string): vscode.Task {
        return DotNetTaskProvider.getDiagnosticTask(processId, DotNetProfilerType.Trace);
    }
    public static getGCDumpTask(processId: number | string): vscode.Task {
        return DotNetTaskProvider.getDiagnosticTask(processId, DotNetProfilerType.GCDump);
    }

    private static getTask(definition: vscode.TaskDefinition, configuration: string | undefined = undefined, framework: string | undefined = undefined): vscode.Task {
        const options: vscode.ShellExecutionOptions = {
            cwd: Extensions.getCurrentWorkingDirectory(),
            env: Extensions.getSetting<{ [key: string]: string }>(res.configIdMSBuildAdditionalEnvironment)
        };
        const builder = new ProcessArgumentBuilder(Interop.dotnetPath)
            .append(definition.target).append(Extensions.toUnixPath(definition.project) /*DotRush/issues/88*/)
            .conditional(`-p:Configuration=${configuration}`, () => configuration !== undefined)
            .conditional(`-p:TargetFramework=${framework}`, () => framework !== undefined);

        if (definition.target === DotNetTarget.Build) {
            builder.conditional('--no-restore', () => Extensions.getSetting<boolean>(res.configIdMSBuildNoRestore, false));
            builder.conditional('--no-dependencies', () => Extensions.getSetting<boolean>(res.configIdMSBuildNoDependencies, false));
            Extensions.getSetting<string[]>(res.configIdMSBuildAdditionalBuildArguments)?.forEach(arg => builder.override(arg));
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
    private static getDiagnosticTask(processId: number | string, profilerType: DotNetProfilerType): vscode.Task {
        const options: vscode.ShellExecutionOptions = { cwd: Extensions.getCurrentWorkingDirectory() };
        const toolPath = path.join(Interop.binariesPath, 'Diagnostics', `dotnet-${profilerType}.dll`);
        const builder = new ProcessArgumentBuilder(Interop.dotnetPath)
            .append(toolPath)
            .append('collect')
            .append('-p').append(processId.toString());

        if (profilerType === DotNetProfilerType.Trace) {
            builder.append('--format').append('speedscope');
        }

        return new vscode.Task(
            { type: res.taskDefinitionId },
            vscode.TaskScope.Workspace,
            'Profile',
            res.extensionId,
            new vscode.ShellExecution(builder.getCommand(), builder.getArguments(), options),
        );
    }
}
