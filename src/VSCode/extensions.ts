import { ProjectOrSolutionItem } from './models/project';
import * as res from './resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class Extensions {
    public static readonly projectExtPattern: string = '.csproj';
    public static readonly solutionExtPattern: string = '.{sln,slnf,slnx}';

    public static getSetting<TValue>(id: string, fallback: TValue | undefined = undefined): TValue | undefined {
        return vscode.workspace.getConfiguration(res.extensionId).get<TValue>(id) ?? fallback;
    }
    public static putSetting<TValue>(id: string, value: TValue, target: vscode.ConfigurationTarget): Thenable<void> {
        return vscode.workspace.getConfiguration(res.extensionId).update(id, value, target);
    }
    public static onVSCode<TValue>(official: TValue, fork: TValue): TValue {
        return vscode.env.appName.includes(res.vscodeAppName) ? official : fork;
    }

    public static async getProjectFiles(): Promise<string[]> {
        return (await Extensions.findFiles(undefined, Extensions.projectExtPattern)).map(x => x.fsPath);
    }
    public static async getSolutionFiles(): Promise<string[]> {
        return (await Extensions.findFiles(undefined, Extensions.solutionExtPattern)).map(x => x.fsPath);
    }
    public static async selectProjectOrSolutionFile(baseUri: vscode.Uri | undefined = undefined): Promise<string | undefined> {
        if (baseUri?.fsPath !== undefined && path.extname(baseUri?.fsPath).startsWith('.sln'))
            return baseUri.fsPath;
        if (baseUri?.fsPath !== undefined && path.extname(baseUri?.fsPath) === '.csproj')
            return baseUri.fsPath;

        const solutionFiles = await Extensions.findFiles(baseUri, Extensions.solutionExtPattern);
        const projectFiles = await Extensions.findFiles(baseUri, Extensions.projectExtPattern);
        if (projectFiles.length === 0 && solutionFiles.length === 0) {
            vscode.window.showErrorMessage(res.messageNoProjectFileFound);
            return undefined;
        }
        if (solutionFiles.length === 1)
            return solutionFiles[0].fsPath;
        if (projectFiles.length === 1)
            return projectFiles[0].fsPath;

        const items: vscode.QuickPickItem[] = [];
        if (solutionFiles.length > 0) {
            items.push(ProjectOrSolutionItem.solutionSeparator);
            items.push(...solutionFiles.map(it => new ProjectOrSolutionItem(it.fsPath)));
        }
        if (projectFiles.length > 0) {
            items.push(ProjectOrSolutionItem.projectSeparator);
            items.push(...projectFiles.map(it => new ProjectOrSolutionItem(it.fsPath)));
        }
        const selectedItem = await vscode.window.showQuickPick<any>(items, { placeHolder: res.messageSelectProjectTitle });
        return selectedItem?.item;
    }
    public static async selectProjectOrSolutionFiles(baseUri: vscode.Uri | undefined = undefined): Promise<string[] | undefined> {
        const solutionFiles = await Extensions.findFiles(baseUri, Extensions.solutionExtPattern);
        const projectFiles = await Extensions.findFiles(baseUri, Extensions.projectExtPattern);
        if (projectFiles.length === 0 && solutionFiles.length === 0) {
            vscode.window.showErrorMessage(res.messageNoProjectFileFound);
            return undefined;
        }

        const items: vscode.QuickPickItem[] = [];
        if (solutionFiles.length > 0) {
            items.push(ProjectOrSolutionItem.solutionSeparator);
            items.push(...solutionFiles.map(it => new ProjectOrSolutionItem(it.fsPath)));
        }
        if (projectFiles.length > 0) {
            items.push(ProjectOrSolutionItem.projectSeparator);
            items.push(...projectFiles.map(it => new ProjectOrSolutionItem(it.fsPath)));
        }
        const selectedItems = await vscode.window.showQuickPick(items, { canPickMany: true, placeHolder: res.messageSelectTargetTitle });
        return selectedItems?.map((it: any) => it.item);
    }
    public static async selectProjecFile(baseUri: vscode.Uri | undefined = undefined): Promise<string | undefined> {
        if (baseUri?.fsPath !== undefined && path.extname(baseUri?.fsPath) === '.csproj')
            return baseUri.fsPath;

        const projectFiles = await Extensions.findFiles(baseUri, Extensions.projectExtPattern);
        if (projectFiles.length === 0) {
            vscode.window.showErrorMessage(res.messageNoProjectFileFound);
            return undefined;
        }
        if (projectFiles.length === 1)
            return projectFiles[0].fsPath;

        const items = projectFiles.map(it => new ProjectOrSolutionItem(it.fsPath));
        const selectedItem = await vscode.window.showQuickPick<any>(items, { placeHolder: res.messageSelectProjectTitle });
        return selectedItem?.item;
    }

    public static async parallelForEach<T>(items: T[], action: (item: T) => Promise<void>): Promise<void> {
        const parallel = 4;
        for (let i = 0; i < items.length; i += parallel) {
            const slice = items.slice(i, i + parallel);
            await Promise.all(slice.map(action));
        }
    }
    public static async waitForTask(task: vscode.Task): Promise<boolean> {
        const execution = await vscode.tasks.executeTask(task);
        const executionExitCode = await new Promise<number>((resolve) => {
            const disposable = vscode.tasks.onDidEndTaskProcess(e => {
                if (e.execution.task === execution.task) {
                    resolve(e.exitCode ?? -1);
                    disposable.dispose();
                }
            });
        });
        return executionExitCode === 0;
    }
    public static async getTask(taskName: string | undefined): Promise<vscode.Task | undefined> {
        if (taskName === undefined)
            return undefined;

        const tasks = await vscode.tasks.fetchTasks();
        return tasks.find(task => task.name === taskName);
    }
    public static getCurrentWorkingDirectory(): string | undefined {
        if (vscode.workspace.workspaceFile !== undefined)
            return path.dirname(vscode.workspace.workspaceFile.fsPath);
        if (vscode.workspace.workspaceFolders !== undefined && vscode.workspace.workspaceFolders.length > 0)
            return vscode.workspace.workspaceFolders[0].uri.fsPath;

        return undefined;
    }
    public static getWorkspaceFolder(): vscode.WorkspaceFolder | undefined {
        if (vscode.workspace.workspaceFolders !== undefined && vscode.workspace.workspaceFolders.length === 1)
            return vscode.workspace.workspaceFolders[0];
        return undefined;
    }
    public static deserialize<TModel>(json: string): TModel | undefined {
        try {
            return JSON.parse(json) as TModel;
        } catch {
            return undefined;
        }
    }
    public static capitalize(text: string): string {
        return text.charAt(0).toUpperCase() + text.slice(1);
    }

    private static async findFiles(baseUri: vscode.Uri | undefined, extension: string): Promise<vscode.Uri[]> {
        if (baseUri?.fsPath !== undefined && path.extname(baseUri.fsPath) === extension)
            return [baseUri];

        const result = baseUri?.fsPath === undefined
            ? await vscode.workspace.findFiles(`**/*${extension}`, null)
            : await vscode.workspace.findFiles(new vscode.RelativePattern(baseUri, `**/*${extension}`), null);

        return result.sort((a, b) => path.basename(a.fsPath).localeCompare(path.basename(b.fsPath)));
    }
}