import { DotNetTaskProvider } from './../providers/dotnetTaskProvider';
import { StatusBarController } from './statusbarController';
import { Extensions } from '../extensions';
import * as res from './../resources/constants';
import * as vscode from 'vscode';

export class ContextMenuController {
    public static activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdBuildProject, async (path: vscode.Uri) => {
            const projectFile = await Extensions.selectProjectOrSolutionFile(path);
            if (projectFile !== undefined)
                vscode.tasks.executeTask(DotNetTaskProvider.getBuildTask(projectFile));
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdRestoreProject, async (path: vscode.Uri) => {
            const projectFile = await Extensions.selectProjectOrSolutionFile(path);
            if (projectFile !== undefined)
                vscode.tasks.executeTask(DotNetTaskProvider.getRestoreTask(projectFile));
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdCleanProject, async (path: vscode.Uri) => {
            const projectFile = await Extensions.selectProjectOrSolutionFile(path);
            if (projectFile !== undefined)
                vscode.tasks.executeTask(DotNetTaskProvider.getCleanTask(projectFile));
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdSetStartupProject, async (path: vscode.Uri) => {
            const projectFile = await Extensions.selectProjecFile(path);
            if (projectFile !== undefined)
                StatusBarController.updateStatusBarState(projectFile);
        }));

        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdBuildWorkspace, async () => {
            let projectOrSolutionFiles = Extensions.getSetting<string[]>(res.configIdRoslynProjectOrSolutionFiles);
            if (projectOrSolutionFiles === undefined || projectOrSolutionFiles.length === 0)
                projectOrSolutionFiles = await Extensions.selectProjectOrSolutionFiles();

            if (projectOrSolutionFiles === undefined || projectOrSolutionFiles.length === 0)
                return;

            for (const targetFile of projectOrSolutionFiles) {
                const executionSuccess = await Extensions.waitForTask(DotNetTaskProvider.getBuildTask(targetFile));
                if (!executionSuccess)
                    break;
            }
        }));
    }
}
