import { ProjectOrSolutionItem } from './models/project';
import * as res from './resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

interface IFilter {
    regex: RegExp;
    pattern: string
}

export class Extensions {
    public static readonly projectFilter: IFilter = {
        regex: new RegExp('.*\\.(csproj|fsproj|vbproj)$'),
        pattern: '*.{csproj,fsproj,vbproj}'
    }
    public static readonly csProjectFilter: IFilter = {
        regex: new RegExp('.*\\.(csproj)$'),
        pattern: '*.csproj'
    }
    public static readonly solutionFilter: IFilter = {
        regex: new RegExp('.*\\.(sln|slnf|slnx)$'),
        pattern: '*.{sln,slnf,slnx}'
    }

    public static getSetting<TValue>(id: string, fallback?: TValue): TValue | undefined {
        return vscode.workspace.getConfiguration(res.extensionId).get<TValue>(id) ?? fallback;
    }
    public static putSetting<TValue>(id: string, value: TValue, target: vscode.ConfigurationTarget): Thenable<void> {
        return vscode.workspace.getConfiguration(res.extensionId).update(id, value, target);
    }
    public static onVSCode<TValue>(official: TValue, fork: TValue): TValue {
        return vscode.env.appName.includes(res.vscodeAppName) ? official : fork;
    }

    public static async getProjectFiles(csharpOnly: boolean = false): Promise<string[]> {
        const filter = csharpOnly ? Extensions.csProjectFilter : Extensions.projectFilter;
        return (await Extensions.findFiles(undefined, filter)).map(x => x.fsPath);
    }
    public static async getSolutionFiles(): Promise<string[]> {
        return (await Extensions.findFiles(undefined, Extensions.solutionFilter)).map(x => x.fsPath);
    }
    public static isProjectFile(filePath?: string, csharpOnly: boolean = false): boolean {
        if (filePath === undefined)
            return false;
        const extPattern = csharpOnly ? Extensions.csProjectFilter.regex : Extensions.projectFilter.regex;
        return path.extname(filePath).match(extPattern) !== null;
    }
    public static isSolutionFile(filePath?: string): boolean {
        if (filePath === undefined)
            return false;
        return path.extname(filePath).match(Extensions.solutionFilter.regex) !== null;
    }

    public static async selectProjectOrSolutionFile(baseUri?: vscode.Uri, csharpOnly: boolean = false): Promise<string | undefined> {
        if (baseUri?.fsPath !== undefined && Extensions.isSolutionFile(baseUri?.fsPath))
            return baseUri.fsPath;
        if (baseUri?.fsPath !== undefined && Extensions.isProjectFile(baseUri?.fsPath, csharpOnly))
            return baseUri.fsPath;

        const solutionFiles = await Extensions.findFiles(baseUri, Extensions.solutionFilter);
        const projectFiles = await Extensions.findFiles(baseUri, csharpOnly ? Extensions.csProjectFilter : Extensions.projectFilter);
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
    public static async selectProjectOrSolutionFiles(baseUri?: vscode.Uri, csharpOnly: boolean = false): Promise<string[] | undefined> {
        const solutionFiles = await Extensions.findFiles(baseUri, Extensions.solutionFilter);
        const projectFiles = await Extensions.findFiles(baseUri, csharpOnly ? Extensions.csProjectFilter : Extensions.projectFilter);
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
        const selectedItems = await vscode.window.showQuickPick(items, { canPickMany: true, placeHolder: res.messageSelectTargetTitle, ignoreFocusOut: true });
        return selectedItems?.map((it: any) => it.item);
    }
    public static async selectProjecFile(baseUri?: vscode.Uri, csharpOnly: boolean = false): Promise<string | undefined> {
        if (baseUri?.fsPath !== undefined && Extensions.isProjectFile(baseUri?.fsPath, csharpOnly))
            return baseUri.fsPath;

        const projectFiles = await Extensions.findFiles(baseUri, csharpOnly ? Extensions.csProjectFilter : Extensions.projectFilter);
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
    public static async waitForWorkspaceFoldersChange(): Promise<void> {
        return new Promise<void>((resolve) => {
            const disposable = vscode.workspace.onDidChangeWorkspaceFolders(e => {
                resolve();
                disposable.dispose();
            });
        });
    }
    public static async getTask(taskName?: string): Promise<vscode.Task | undefined> {
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
    public static toUnixPath(filePath: string): string {
        return filePath.replace(/\\/g, '/');
    }

    public static documentIdFromUri(uri?: vscode.Uri): any {
        return { uri: uri?.toString() }
    }
    public static documentIdFromDocument(document?: vscode.TextDocument): any {
        return { uri: document?.uri?.toString() };
    }

    private static async findFiles(baseUri: vscode.Uri | undefined, filter: IFilter): Promise<vscode.Uri[]> {
        if (baseUri?.fsPath !== undefined && baseUri.fsPath.match(filter.regex) !== null)
            return [baseUri];

        const result = baseUri?.fsPath === undefined
            ? await vscode.workspace.findFiles(`**/${filter.pattern}`, null)
            : await vscode.workspace.findFiles(new vscode.RelativePattern(baseUri, `**/${filter.pattern}`), null);

        return result.sort((a, b) => path.basename(a.fsPath).localeCompare(path.basename(b.fsPath)));
    }
}