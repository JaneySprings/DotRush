import { StateController } from './stateController';
import { Project, TargetFrameworkItem } from '../models/project';
import { Interop } from "../interop/interop";
import { Icons } from '../resources/icons';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';

export class StatusBarController {
    private static projectStatusBarItem: vscode.StatusBarItem;
    private static configurationStatusBarItem: vscode.StatusBarItem;
    private static projectDecorationProvider: StartupProjectDecorationProvider;

    public static project: Project | undefined;
    public static configuration: string | undefined;
    public static framework: string | undefined;

    public static async activate(context: vscode.ExtensionContext): Promise<void> {
        if (vscode.extensions.getExtension(res.extensionMeteorId) !== undefined) {
            const exports = await vscode.extensions.getExtension(res.extensionMeteorId)?.activate();
            exports?.onActiveProjectChanged?.add((p: Project) => StatusBarController.project = p);
            exports?.onActiveConfigurationChanged?.add((c: string) => StatusBarController.configuration = c);
            exports?.onActiveFrameworkChanged?.add((f: string) => StatusBarController.framework = f);
            return;
        }

        StatusBarController.projectStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
        StatusBarController.configurationStatusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 99);
        StatusBarController.projectDecorationProvider = new StartupProjectDecorationProvider();

        StatusBarController.configurationStatusBarItem.command = res.commandIdSelectActiveConfiguration;
        StatusBarController.projectStatusBarItem.command = res.commandIdSelectActiveProject;

        context.subscriptions.push(StatusBarController.projectStatusBarItem);
        context.subscriptions.push(StatusBarController.configurationStatusBarItem);
        context.subscriptions.push(vscode.window.registerFileDecorationProvider(StatusBarController.projectDecorationProvider));

        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdActiveProjectPath, () => StatusBarController.project?.path));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdActiveConfiguration, () => StatusBarController.configuration));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdSelectActiveProject, StatusBarController.showQuickPickFramework));
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdSelectActiveConfiguration, StatusBarController.showQuickPickConfiguration));
        context.subscriptions.push(vscode.workspace.onDidChangeWorkspaceFolders(StatusBarController.update));
        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(ev => {
            if (ev.fileName.endsWith('proj') || ev.fileName.endsWith('.props'))
                StatusBarController.update();
        }));

        StatusBarController.update();
        vscode.commands.executeCommand('setContext', res.commandIdStatusBarEnabled, true);
    }
    public static async update() : Promise<void> {
        let projectPath = StateController.getLocal<string>('project');
        let configuration = StateController.getLocal<string>('configuration');
        let framework = StateController.getLocal<string>('framework');
        if (projectPath === undefined || !fs.existsSync(projectPath)) {
            projectPath = (await vscode.workspace.findFiles('**/*.csproj', undefined, 1))[0]?.fsPath;
            configuration = undefined;
            framework = undefined;
        }
        
        if (projectPath === undefined) {
            StatusBarController.projectStatusBarItem.hide();
            StatusBarController.configurationStatusBarItem.hide();
            StatusBarController.projectDecorationProvider.update(undefined);
            return;
        }

        StatusBarController.performSelectProject(await Interop.getProject(projectPath));
        StatusBarController.performSelectFramework(framework);
        StatusBarController.performSelectConfiguration(configuration);
        StatusBarController.projectStatusBarItem.show();
        StatusBarController.configurationStatusBarItem.show();
        StatusBarController.projectDecorationProvider.update(StatusBarController.project);
    }

    public static performSelectProject(item: Project) {
        StatusBarController.project = item;
        StatusBarController.framework = item.frameworks[0];
        StatusBarController.projectStatusBarItem.text = `${Icons.target} ${StatusBarController.project?.name} | ${StatusBarController.framework}`;
        StatusBarController.projectDecorationProvider.update(item);
        StateController.putLocal('project', StatusBarController.project.path);
    }
    public static performSelectFramework(item: string | undefined = undefined) {
        StatusBarController.framework = item ?? StatusBarController.project?.frameworks[0];
        StatusBarController.projectStatusBarItem.text = `${Icons.target} ${StatusBarController.project?.name} | ${StatusBarController.framework}`;
        StateController.putLocal('framework', StatusBarController.framework);
    }
    public static performSelectConfiguration(item: string | undefined = undefined) {
        StatusBarController.configuration = item ?? 'Debug';
        StatusBarController.configurationStatusBarItem.text = `${Icons.target} ${StatusBarController.configuration} | Any CPU`;
        StateController.putLocal('configuration', StatusBarController.configuration);
    }

    public static async showQuickPickFramework() {
        if (StatusBarController.project === undefined)
            return;

        const project = StatusBarController.project;
        const items = project.frameworks.map(f => new TargetFrameworkItem(f, project.name));
        const options = { placeHolder: res.commandTitleSelectActiveFramework };
        const selectedItem = await vscode.window.showQuickPick(items, options);

        if (selectedItem !== undefined) 
            StatusBarController.performSelectFramework(selectedItem.item);
    }
    public static async showQuickPickConfiguration() {
        const items = StatusBarController.project?.configurations ?? [];
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