import * as res from './resources';
import * as vscode from 'vscode';
import * as path from 'path';

export class CommandsController {
    public static activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.commands.registerCommand(res.taskCommandBuildId, async (path: vscode.Uri) => {
            const projectFile = await CommandsController.selectProjectFile(path);
            const task = await DotNetTaskProvider.getTask("build --no-restore", projectFile);
            if (task !== undefined) 
                vscode.tasks.executeTask(task);
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.taskCommandRestoreId, async (path: vscode.Uri) => {
            const projectFile = await CommandsController.selectProjectFile(path);
            const task = await DotNetTaskProvider.getTask("restore", projectFile);
            if (task !== undefined) 
                vscode.tasks.executeTask(task);
        }));
        context.subscriptions.push(vscode.commands.registerCommand(res.taskCommandCleanId, async (path: vscode.Uri) => {
            const projectFile = await CommandsController.selectProjectFile(path);
            if (projectFile === undefined)
                return;
            await vscode.workspace.fs.delete(vscode.Uri.joinPath(projectFile, "..", "bin"), { recursive: true });
            await vscode.workspace.fs.delete(vscode.Uri.joinPath(projectFile, "..", "obj"), { recursive: true });
        }));
    }

    private static async selectProjectFile(targetUri: vscode.Uri): Promise<vscode.Uri | undefined> {
        if (targetUri.fsPath.endsWith(".csproj"))
            return targetUri;

        const files = await vscode.workspace.findFiles(new vscode.RelativePattern(targetUri, '**/*.csproj'));
        if (files.length === 0) {
            vscode.window.showErrorMessage(res.messaeNoProjectFileFound);
            return undefined;
        }
        if (files.length === 1)
            return files[0];

        const selectedItem = await vscode.window.showQuickPick(files.map(it => new ProjectItem(it)), { placeHolder: res.messageSelectProjectFile });
        return selectedItem?.uri;
    }
}

class DotNetTaskProvider {
    public static async getTask(target: string, projectFile: vscode.Uri | undefined): Promise<vscode.Task | undefined> { 
        if (projectFile === undefined)
            return undefined;
        
        const command = `dotnet ${target} "${projectFile.fsPath}"`;
        return new vscode.Task({ type: `${res.extensionId}.task` },
            vscode.TaskScope.Workspace, target, res.extensionId, new vscode.ShellExecution(command), res.microsoftProblemMatcherId
        );
    }
}

class ProjectItem implements vscode.QuickPickItem {
    label: string;
    uri: vscode.Uri;

    constructor(projectPath: vscode.Uri) {
        this.label = path.basename(projectPath.fsPath);
        this.uri = projectPath;
    }
}