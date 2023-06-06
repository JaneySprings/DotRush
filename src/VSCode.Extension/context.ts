import { getSetting } from './extension';
import * as res from './resources';
import * as vscode from 'vscode';


export class ContextMenuController {
    public static activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdBuild, async (path: vscode.Uri) => {
            const task = await DotNetTaskProvider.getTask("build", path);
            if (task !== undefined) 
                vscode.tasks.executeTask(task);
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdRebuild, async (path: vscode.Uri) => {
            vscode.workspace.fs.delete(vscode.Uri.joinPath(path, "bin"), { recursive: true });
            vscode.workspace.fs.delete(vscode.Uri.joinPath(path, "obj"), { recursive: true });

            const task = await DotNetTaskProvider.getTask("build", path);
            if (task !== undefined) 
                vscode.tasks.executeTask(task);
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdClean, (path: vscode.Uri) => {
            vscode.workspace.fs.delete(vscode.Uri.joinPath(path, "bin"), { recursive: true });
            vscode.workspace.fs.delete(vscode.Uri.joinPath(path, "obj"), { recursive: true });
        }));
    }
}

class DotNetTaskProvider {
    public static async getTask(target: string, directory: vscode.Uri): Promise<vscode.Task | undefined> { 
        const files = await vscode.workspace.fs.readDirectory(directory);
        const csprojName = files.find((file) => file[0].endsWith(".csproj"));
        if (csprojName === undefined)
            return undefined;
        
        const csproj = vscode.Uri.joinPath(directory, csprojName[0]);
        const args = getSetting<string>(res.configIdAdditionalMSBuildArgs) ?? '';
        const command = `dotnet ${target} ${csproj.fsPath} ${args}`;
        return new vscode.Task({ type: `${res.extensionId}.${res.taskDefinitionId}` }, 
            vscode.TaskScope.Workspace, target, res.extensionId, new vscode.ShellExecution(command)
        );
    }
}