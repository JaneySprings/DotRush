import { SolutionController } from '../csharp/solutionController';
import { ClientController } from '../csharp/clientController';
import { DotNetTaskProvider } from './dotnetTaskProvider';
import { Configuration } from '../configuration';
import * as res from '../resources';
import * as vscode from 'vscode';


export class ContextMenuController {
    public static activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdBuild, (path: vscode.Uri) => {
            const useFramework = Configuration.getSetting<boolean>(res.configurationIdBuildOnlyFramework);
            const task = DotNetTaskProvider.getTask("build", path.fsPath, useFramework);
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

        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdAddToSln, async(path: vscode.Uri) => {
            const projectPath = await SolutionController.findProject(path.fsPath);
            if (projectPath !== undefined)
                SolutionController.addProject(ClientController.currentTargetDirectory, projectPath);
        }));
    }
}