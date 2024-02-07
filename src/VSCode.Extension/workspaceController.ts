import { ServerController } from './serverController';
import { ProjectItem } from './models/projectItem';
import { Workspace } from './models/workspace';
import * as res from './resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class WorkspaceController {
    public static targets: Workspace[];

    public static async activate(context: vscode.ExtensionContext) {
        await WorkspaceController.updateWorkspaceAsync();

        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async e => {
            if (path.extname(e.fileName) === '.csproj' && WorkspaceController.targets.find(it => it.projects.includes(e.fileName)))
                WorkspaceController.showReloadDialogAsync(res.messageProjectChanged);
        }));
        context.subscriptions.push(vscode.workspace.onDidChangeWorkspaceFolders(async e => {
            const removed = e.removed.map(it => it.uri.fsPath);
            const hasRemoveChanges = WorkspaceController.targets.filter(it => removed.includes(it.path)).flatMap(it => it.projects).length !== 0;

            await WorkspaceController.updateWorkspaceAsync();

            const added = e.added.map(it => it.uri.fsPath);
            const hasAddChanges = WorkspaceController.targets.filter(it => added.includes(it.path)).flatMap(it => it.projects).length !== 0;
           
            if (hasRemoveChanges || hasAddChanges)
                ServerController.restart();
        }));

        if (WorkspaceController.targets.length !== 0)
            ServerController.start();
    }

    private static async updateWorkspaceAsync() { 
        WorkspaceController.targets = [];
        for (const folder of vscode.workspace.workspaceFolders ?? []) {
            const workspaceItem = await WorkspaceController.getWorkspaceItemAsync(folder.uri.fsPath);
            WorkspaceController.targets.push(workspaceItem);
        }
    }
    private static async getWorkspaceItemAsync(path: string): Promise<Workspace> {
        const topLevelProjectFiles = await vscode.workspace.findFiles(new vscode.RelativePattern(path, '*.csproj'));
        if (topLevelProjectFiles.length === 1)
            return new Workspace(path, topLevelProjectFiles.map(it => it.fsPath));
        if (topLevelProjectFiles.length > 1)
            return new Workspace(path, (await WorkspaceController.selectTargetsAsync(path, topLevelProjectFiles)));

        const allProjectFiles = await vscode.workspace.findFiles(new vscode.RelativePattern(path, '**/*.csproj'));
        if (allProjectFiles.length === 1)
            return new Workspace(path, allProjectFiles.map(it => it.fsPath));
        if (allProjectFiles.length === 0)
            return new Workspace(path);
        return new Workspace(path, (await WorkspaceController.selectTargetsAsync(path, allProjectFiles)));
    }

    private static async showReloadDialogAsync(reason: string) {
        const value = await vscode.window.showWarningMessage(reason, res.messageReload)
        if (value === res.messageReload)
            ServerController.restart();
    }
    private static async selectTargetsAsync(target: string, paths: vscode.Uri[]): Promise<string[]> {
        const workspaceItem = WorkspaceController.targets.find(it => it.path === target);
        if (workspaceItem !== undefined)
            return workspaceItem.projects;

        const result = await vscode.window.showQuickPick(paths.map(it => new ProjectItem(it)), { 
            placeHolder: res.messageSelectProjects.replace('$', path.basename(target)),
            canPickMany: true
        });
        return result?.map(it => it.uri.fsPath) ?? [];
    }
}