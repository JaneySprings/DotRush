import { Interop } from "../interop/interop";
import { StateController } from './stateController';
import { PublicExports } from '../publicExports';
import { Project, ProjectItem } from '../models/project';
import { Icons } from '../resources/icons';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

export class StatusBarController {
    private static projectStatusBarItem: vscode.StatusBarItem;
    private static configurationStatusBarItem: vscode.StatusBarItem;

    public static projects: Project[];
    public static project: Project | undefined;
    public static configuration: string | undefined;

    public static activate(context: vscode.ExtensionContext) {
        StatusBarController.projectStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
        StatusBarController.configurationStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 99);

        StatusBarController.configurationStatusBarItem.command = res.commandIdSelectActiveConfiguration;
        StatusBarController.projectStatusBarItem.command = res.commandIdSelectActiveProject;

        context.subscriptions.push(StatusBarController.projectStatusBarItem);
        context.subscriptions.push(StatusBarController.configurationStatusBarItem);

        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdActiveProjectPath, () => StatusBarController.project?.path));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdActiveConfiguration, () => StatusBarController.configuration));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdSelectActiveProject, StatusBarController.showQuickPickProject));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdSelectActiveConfiguration, StatusBarController.showQuickPickConfiguration));
        context.subscriptions.push(vscode.workspace.onDidChangeWorkspaceFolders(StatusBarController.update));
        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(ev => {
            if (ev.fileName.endsWith('proj') || ev.fileName.endsWith('.props'))
                StatusBarController.update();
        }));
    }
    public static async update() : Promise<void> {
        const folders = vscode.workspace.workspaceFolders!.map(it => it.uri.fsPath);
        StatusBarController.projects = await Interop.getProjects(folders);

        if (StatusBarController.projects.length === 0) {
            StatusBarController.projectStatusBarItem.hide();
            StatusBarController.configurationStatusBarItem.hide();
            PublicExports.instance.invokeAll();
            return;
        }
        
        StateController.load();
        StatusBarController.performSelectProject(StatusBarController.project);
        StatusBarController.performSelectConfiguration(StatusBarController.configuration);
        StatusBarController.projectStatusBarItem.show();
        StatusBarController.configurationStatusBarItem.show();
    }

    public static performSelectProject(item: Project | undefined = undefined) {
        StatusBarController.project = item ?? StatusBarController.projects[0];
        StatusBarController.projectStatusBarItem.text = `${Icons.project} ${StatusBarController.project?.name}`;
        PublicExports.instance.projectChangedEventHandler.invoke(StatusBarController.project);
        StateController.saveProject();
    }
    public static performSelectConfiguration(item: string | undefined = undefined) {
        StatusBarController.configuration = item ?? 'Debug';
        StatusBarController.configurationStatusBarItem.text = `${Icons.target} ${StatusBarController.configuration} | Any CPU`;
        PublicExports.instance.configurationChangedEventHandler.invoke(StatusBarController.configuration);
        StateController.saveConfiguration();
    }

    public static async showQuickPickProject() {
        const items = StatusBarController.projects.map(project => new ProjectItem(project));
        const options = { placeHolder: res.commandTitleSelectActiveProject };
        const selectedItem = await vscode.window.showQuickPick(items, options);

        if (selectedItem !== undefined) {
            StatusBarController.performSelectProject(selectedItem.item);
            StatusBarController.performSelectConfiguration(undefined);
        }
    }
    public static async showQuickPickConfiguration() {
        const items = StatusBarController.project?.configurations ?? [];
        const options = { placeHolder: res.commandTitleSelectActiveConfiguration };
        const selectedItem = await vscode.window.showQuickPick(items, options);
        
        if (selectedItem !== undefined)
            StatusBarController.performSelectConfiguration(selectedItem);
    }
}