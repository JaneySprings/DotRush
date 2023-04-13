import * as res from './resources';
import * as vscode from 'vscode';


export class ContextMenuController {
    public static activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdBuild, (path: vscode.Uri) => {
            const task = DotNetTaskProvider.getTask("build", path.fsPath);
            if (task !== undefined) 
                vscode.tasks.executeTask(task);
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdRestore, (path: vscode.Uri) => {
            const task = DotNetTaskProvider.getTask("restore", path.fsPath);
            if (task !== undefined) 
                vscode.tasks.executeTask(task);
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdClean, (path: vscode.Uri) => {
            const task = DotNetTaskProvider.getTask("clean", path.fsPath);
            if (task !== undefined) 
                vscode.tasks.executeTask(task);
        }));
    }
}

class DotNetTaskProvider {
    public static getTask(target: string, directory: string): vscode.Task | undefined { 
        const command = `dotnet ${target} ${directory}`;
        return new vscode.Task({ type: `${res.extensionId}.${res.taskDefinitionId}` }, 
            vscode.TaskScope.Workspace, target, res.extensionId, new vscode.ShellExecution(command)
        );
    }
}