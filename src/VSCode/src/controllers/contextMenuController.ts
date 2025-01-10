import { DotNetTaskProvider } from './../providers/dotnetTaskProvider';
import { StatusBarController } from './statusbarController';
import { ProjectOrSolutionItem } from './../models/project';
import { Interop } from '../interop/interop';
import * as res from './../resources/constants';
import * as vscode from 'vscode';

export class ContextMenuController {
    public static async activate(context: vscode.ExtensionContext): Promise<void> {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdBuildProject, async (path: vscode.Uri) => {
            const projectFile = await ContextMenuController.selectProjectFileAsync(path);
            if (projectFile !== undefined)
                vscode.tasks.executeTask(DotNetTaskProvider.getBuildTask(projectFile));
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdRestoreProject, async (path: vscode.Uri) => {
            const projectFile = await ContextMenuController.selectProjectFileAsync(path);
            if (projectFile !== undefined)
                vscode.tasks.executeTask(DotNetTaskProvider.getRestoreTask(projectFile));
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdCleanProject, async (path: vscode.Uri) => {
            const projectFile = await ContextMenuController.selectProjectFileAsync(path);
            if (projectFile !== undefined)
                vscode.tasks.executeTask(DotNetTaskProvider.getCleanTask(projectFile));
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdSetStartupProject, async (path: vscode.Uri) => {
            const projectFile = await ContextMenuController.selectProjectFileAsync(path);
            if (projectFile !== undefined)
                StatusBarController.performSelectProject(projectFile);
        }));
    }

    private static async selectProjectFileAsync(targetUri: vscode.Uri): Promise<string | undefined> {
        if (targetUri.fsPath.endsWith(".csproj"))
            return targetUri.fsPath;

        const files = await vscode.workspace.findFiles(new vscode.RelativePattern(targetUri, '**/*.csproj'));
        if (files.length === 0) {
            vscode.window.showErrorMessage(res.messageNoProjectFileFound);
            return undefined;
        }
        if (files.length === 1)
            return files[0].fsPath;

        const selectedItem = await vscode.window.showQuickPick(files.map(it => new ProjectOrSolutionItem(it.fsPath)), { placeHolder: res.messageSelectProjectTitle });
        return selectedItem?.item;
    }
}
