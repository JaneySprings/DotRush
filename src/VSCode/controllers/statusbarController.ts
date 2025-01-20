import { Project, TargetFrameworkItem } from '../models/project';
import { StateController } from './stateController';
import { PublicExports } from '../publicExports';
import { Interop } from "../interop/interop";
import { Extensions } from '../extensions';
import { Icons } from '../resources/icons';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class StatusBarController {
    private static projectStatusBarItem: vscode.StatusBarItem;
    private static configurationStatusBarItem: vscode.StatusBarItem;
    private static projectDecorationProvider: StartupProjectDecorationProvider;

    public static activeProject: Project | undefined;
    public static activeConfiguration: string | undefined;
    public static activeFramework: string | undefined;


    public static async activate(context: vscode.ExtensionContext): Promise<void> {
        StatusBarController.projectStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
        StatusBarController.configurationStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 99);
        StatusBarController.projectDecorationProvider = new StartupProjectDecorationProvider();
        
        StatusBarController.projectStatusBarItem.command = res.commandIdSelectActiveFramework;
        StatusBarController.configurationStatusBarItem.command = res.commandIdSelectActiveConfiguration;

        context.subscriptions.push(StatusBarController.projectStatusBarItem);
        context.subscriptions.push(StatusBarController.configurationStatusBarItem);
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdSelectActiveFramework, StatusBarController.showQuickPickFramework));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdSelectActiveConfiguration, StatusBarController.showQuickPickConfiguration));
        context.subscriptions.push(vscode.window.registerFileDecorationProvider(StatusBarController.projectDecorationProvider));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdActiveProjectPath, () => StatusBarController.activeProject?.path));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdActiveConfiguration, () => StatusBarController.activeConfiguration));
        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async e => {
            if (path.extname(e.fileName) === '.csproj' && StatusBarController.activeProject !== undefined)
                StatusBarController.performSelectProject(StatusBarController.activeProject.path);
        }));
        
        await vscode.commands.executeCommand('setContext', res.commandIdStatusBarEnabled, true);
        await StatusBarController.updateProjectStatusBarItem();
    }
    
    private static async updateProjectStatusBarItem(): Promise<void> {
        const projects = await Extensions.getProjectFiles();
        if (projects.length <= 0)
            return StatusBarController.projectStatusBarItem.hide();

        let projectPath = StateController.getLocal<string>('project');
        if (projectPath === undefined || !projects.includes(projectPath))
            projectPath = projects[0];

        await StatusBarController.performSelectProject(projectPath);
    }
    private static async updateFrameworkStatusBarItem(): Promise<void> {
        if (StatusBarController.activeProject === undefined)
            return;

        const framework = StateController.getLocal<string>('framework');
        const activeFramework = StatusBarController.activeProject.frameworks.find(it => it === framework);
        StatusBarController.performSelectFramework(activeFramework);
        StatusBarController.projectStatusBarItem.show();
    }
    private static async updateConfigurationStatusBarItem(): Promise<void> {
        if (StatusBarController.activeProject === undefined)
            return;

        const configuration = StateController.getLocal<string>('configuration');
        const activeConfiguration = StatusBarController.activeProject.configurations.find(it => it === configuration);
        StatusBarController.performSelectConfiguration(activeConfiguration);
        StatusBarController.configurationStatusBarItem.show();
    }

    public static async performSelectProject(path: string): Promise<void> {
        const item = await Interop.getProject(path);
        StatusBarController.activeProject = item;
        StatusBarController.projectDecorationProvider.update(item);
        PublicExports.instance.onActiveProjectChanged.invoke(StatusBarController.activeProject);
        StateController.putLocal('project', StatusBarController.activeProject.path);
        StatusBarController.updateFrameworkStatusBarItem();
        StatusBarController.updateConfigurationStatusBarItem();
    }
    public static performSelectFramework(item: string | undefined = undefined) {
        StatusBarController.activeFramework = item ?? StatusBarController.activeProject?.frameworks[0];

        if (StatusBarController.activeFramework === undefined)
            StatusBarController.projectStatusBarItem.text = `${Icons.target} ${StatusBarController.activeProject?.name}`;
        else
            StatusBarController.projectStatusBarItem.text = `${Icons.target} ${StatusBarController.activeProject?.name} | ${StatusBarController.activeFramework}`;

        PublicExports.instance.onActiveFrameworkChanged.invoke(StatusBarController.activeFramework);
        StateController.putLocal('framework', StatusBarController.activeFramework);
    }
    public static performSelectConfiguration(item: string | undefined = undefined) {
        StatusBarController.activeConfiguration = item ?? 'Debug';
        StatusBarController.configurationStatusBarItem.text = `${Icons.target} ${StatusBarController.activeConfiguration} | Any CPU`;
        PublicExports.instance.onActiveConfigurationChanged.invoke(StatusBarController.activeConfiguration);
        StateController.putLocal('configuration', StatusBarController.activeConfiguration);
    }

    public static async showQuickPickFramework() {
        if (StatusBarController.activeProject === undefined)
            return;

        const project = StatusBarController.activeProject;
        const items = project.frameworks.map(f => new TargetFrameworkItem(f, project.name));
        const options = { placeHolder: res.commandTitleSelectActiveFramework };
        const selectedItem = await vscode.window.showQuickPick(items, options);

        if (selectedItem !== undefined) 
            StatusBarController.performSelectFramework(selectedItem.item);
    }
    public static async showQuickPickConfiguration() {
        const items = StatusBarController.activeProject?.configurations ?? [];
        const options = { placeHolder: res.commandTitleSelectActiveConfiguration };
        const selectedItem = await vscode.window.showQuickPick(items, options);
        
        if (selectedItem !== undefined)
            StatusBarController.performSelectConfiguration(selectedItem);
    }
}

class StartupProjectDecorationProvider implements vscode.FileDecorationProvider {
    private _onDidChangeFileDecorations: vscode.EventEmitter<vscode.Uri | vscode.Uri[] | undefined> = new vscode.EventEmitter<vscode.Uri | vscode.Uri[] | undefined>();
    private startupProjectUri: vscode.Uri | undefined;
    private startupProjectDirectoryUri: vscode.Uri | undefined;

    public onDidChangeFileDecorations?: vscode.Event<vscode.Uri | vscode.Uri[] | undefined> | undefined = this._onDidChangeFileDecorations.event;
    public provideFileDecoration(uri: vscode.Uri, token: vscode.CancellationToken): vscode.ProviderResult<vscode.FileDecoration> {
        if (this.startupProjectUri === undefined || this.startupProjectDirectoryUri === undefined)
            return undefined;

        if (uri.fsPath !== this.startupProjectUri.fsPath && uri.fsPath !== this.startupProjectDirectoryUri.fsPath)
            return undefined;

        return { 
            badge: '●',
            color: new vscode.ThemeColor('pickerGroup.foreground'), 
            tooltip: 'Startup Project'
        };
    }

    public update(project: Project | undefined) {
        if (project === undefined) {
            this.startupProjectUri = undefined;
            this.startupProjectDirectoryUri = undefined;
            this._onDidChangeFileDecorations.fire(undefined);
            return;
        }

        this.startupProjectUri = vscode.Uri.file(project.path);
        this.startupProjectDirectoryUri = vscode.Uri.file(path.dirname(project.path));
        this._onDidChangeFileDecorations.fire(undefined);
    }
}