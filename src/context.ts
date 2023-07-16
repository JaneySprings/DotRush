import { getSetting } from './extension';
import * as res from './resources';
import * as vscode from 'vscode';


export class ContextMenuController {
    public static activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdBuild, async (path: vscode.Uri) => {
            const args = getSetting<string>(res.configIdadditionalBuildArguments);
            const task = await DotNetTaskProvider.getTask("build", path, args);
            if (task !== undefined) 
                vscode.tasks.executeTask(task);
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdRebuild, async (path: vscode.Uri) => {
            await vscode.workspace.fs.delete(vscode.Uri.joinPath(path, "..", "bin"), { recursive: true });
            await vscode.workspace.fs.delete(vscode.Uri.joinPath(path, "..", "obj"), { recursive: true });

            const args = getSetting<string>(res.configIdadditionalBuildArguments);
            const task = await DotNetTaskProvider.getTask("build", path, args);
            if (task !== undefined) 
                vscode.tasks.executeTask(task);
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdClean, async (path: vscode.Uri) => {
            await vscode.workspace.fs.delete(vscode.Uri.joinPath(path, "..", "bin"), { recursive: true });
            await vscode.workspace.fs.delete(vscode.Uri.joinPath(path, "..", "obj"), { recursive: true });
        }));
        // context.subscriptions.push(vscode.commands.registerCommand(res.commandIdRun, async (path: vscode.Uri) => {
        //     const args = getSetting<string>(res.configIdadditionalRunArgs);
        //     const task = await DotNetTaskProvider.getTask("run", path, args);
        //     if (task !== undefined) 
        //         vscode.tasks.executeTask(task);
        // }));
    }
}

class DotNetTaskProvider {
    public static async getTask(target: string, projectFile: vscode.Uri, additionalArgs: string | undefined): Promise<vscode.Task | undefined> { 
        const args = additionalArgs ?? '';
        const command = `dotnet ${target} "${projectFile.fsPath}" ${args}`;
        return new vscode.Task({ type: `${res.extensionId}.${res.taskDefinitionId}` }, 
            vscode.TaskScope.Workspace, target, res.extensionId, new vscode.ShellExecution(command),
            "$dotrush.problemMatcher"
        );
    }
}