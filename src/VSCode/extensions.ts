import { ProjectOrSolutionItem } from './models/project';
import * as res from './resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class Extensions {
    public static readonly projectExtPattern: string = '.csproj';
    public static readonly solutionExtPattern: string = '.{sln,slnf}';

    public static async getProjectFiles(): Promise<string[]> {
        return (await Extensions.findFiles(undefined, Extensions.projectExtPattern)).map(x => x.fsPath);
    }
    public static async getTestProjectFiles(): Promise<string[]> {
        const projects = await Extensions.getProjectFiles();
        return projects.filter(x => path.basename(x).toLowerCase().includes('test'));
    }
    public static async getSolutionFiles(): Promise<string[]> {
        return (await Extensions.findFiles(undefined, Extensions.solutionExtPattern)).map(x => x.fsPath);
    }
    public static getSetting(id: string, fallback: any = undefined): any {
        return vscode.workspace.getConfiguration(res.extensionId).get(id) ?? fallback;
    }

    public static async selectProjectFile(baseUri: vscode.Uri | undefined = undefined): Promise<string | undefined> {
        const projectFiles = await Extensions.findFiles(baseUri, Extensions.projectExtPattern);
        if (projectFiles.length === 0) {
            vscode.window.showErrorMessage(res.messageNoProjectFileFound);
            return undefined;
        }
        if (projectFiles.length === 1)
            return projectFiles[0].fsPath;

        const items = projectFiles.map(it => new ProjectOrSolutionItem(it.fsPath));
        const selectedItem = await vscode.window.showQuickPick(items, { placeHolder: res.messageSelectProjectTitle });
        return selectedItem?.item;
    }

    public static async parallelForEach<T>(items: T[], action: (item: T) => Promise<void>): Promise<void> {
        const parallel = 4;
        for (let i = 0; i < items.length; i += parallel) {
            const slice = items.slice(i, i + parallel);
            await Promise.all(slice.map(action));
        }
    }

    private static async findFiles(baseUri: vscode.Uri | undefined, extension: string): Promise<vscode.Uri[]> {
        if (baseUri?.fsPath !== undefined && path.extname(baseUri.fsPath) === extension)
            return [baseUri];

        return baseUri?.fsPath === undefined 
            ? await vscode.workspace.findFiles(`**/*${extension}`)
            : await vscode.workspace.findFiles(new vscode.RelativePattern(baseUri, `**/*${extension}`));
    }
}