import { DotNetTaskProvider } from './../providers/dotnetTaskProvider';
import { StatusBarController } from './statusbarController';
import { Extensions } from '../extensions';
import * as res from './../resources/constants';
import * as vscode from 'vscode';

export class ContextMenuController {
    public static activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdBuildProject, async (path: vscode.Uri) => {
            const projectFile = await Extensions.selectProjectFile(path);
            if (projectFile !== undefined)
                vscode.tasks.executeTask(DotNetTaskProvider.getBuildTask(projectFile));
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdRestoreProject, async (path: vscode.Uri) => {
            const projectFile = await Extensions.selectProjectFile(path);
            if (projectFile !== undefined)
                vscode.tasks.executeTask(DotNetTaskProvider.getRestoreTask(projectFile));
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdCleanProject, async (path: vscode.Uri) => {
            const projectFile = await Extensions.selectProjectFile(path);
            if (projectFile !== undefined)
                vscode.tasks.executeTask(DotNetTaskProvider.getCleanTask(projectFile));
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdSetStartupProject, async (path: vscode.Uri) => {
            const projectFile = await Extensions.selectProjectFile(path);
            if (projectFile !== undefined)
                StatusBarController.performSelectProject(projectFile);
        }));
    }
}
