import { Icons } from '../resources/icons';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

// The 'module' event body the debugger sends for every loaded assembly
interface DebugModule {
    id: number;
    name: string;
    path?: string;
    version?: string;
    symbolStatus?: string;
    symbolFilePath?: string;
    isOptimized?: boolean;
    isUserCode?: boolean;
}

type ModuleTreeNode = DebugModule | ModuleProperty;

export class ModulesView implements vscode.TreeDataProvider<ModuleTreeNode>, vscode.DebugAdapterTrackerFactory {
    public static feature: ModulesView = new ModulesView();

    private loadedModules: DebugModule[] = [];
    private treeViewDataChangedEmitter = new vscode.EventEmitter<void>();
    public readonly onDidChangeTreeData = this.treeViewDataChangedEmitter.event;

    public activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.window.registerTreeDataProvider(res.extendedViewIdModules, this));
        context.subscriptions.push(vscode.debug.registerDebugAdapterTrackerFactory(res.debuggerNetCoreId, this));
        context.subscriptions.push(vscode.debug.registerDebugAdapterTrackerFactory(res.debuggerUnityId, this));
        context.subscriptions.push(vscode.debug.onDidStartDebugSession(() => this.clearModules(), this));
    }

    public getChildren(element?: ModuleTreeNode): vscode.ProviderResult<ModuleTreeNode[]> {
        if (element === undefined)
            return this.loadedModules;
        if (element instanceof ModuleProperty)
            return undefined;

        const properties = [];
        if (element.path)
            properties.push(new ModuleProperty('Path:', element.path));
        if (element.version)
            properties.push(new ModuleProperty('Version:', element.version));
        if (element.symbolStatus)
            properties.push(new ModuleProperty('Symbols:', element.symbolStatus));
        if (element.symbolFilePath)
            properties.push(new ModuleProperty('Symbol File:', element.symbolFilePath));

        properties.push(new ModuleProperty('Optimized:', element.isOptimized ? 'Yes' : 'No'));
        properties.push(new ModuleProperty('User Code:', element.isUserCode ? 'Yes' : 'No'));
        return properties;
    }
    public getTreeItem(element: ModuleTreeNode): vscode.TreeItem {
        if (element instanceof ModuleProperty) {
            const item = new vscode.TreeItem(element.key);
            item.description = element.value;
            item.tooltip = element.value;
            return item;
        }

        const item = new vscode.TreeItem(element.name, vscode.TreeItemCollapsibleState.Collapsed);
        item.iconPath = Icons.moduleIcon;
        return item;
    }
    public createDebugAdapterTracker(session: vscode.DebugSession): vscode.ProviderResult<vscode.DebugAdapterTracker> {
        const treeView = this;
        return {
            onDidSendMessage(message: any) {
                if (message.type != 'event' || message.event != 'module')
                    return;
                if (message.body.reason != 'new')
                    return;

                const module: DebugModule = message.body.module;
                if (treeView.loadedModules.some(it => it.id == module.id))
                    return;

                treeView.loadedModules.push(module);
                treeView.treeViewDataChangedEmitter.fire();
            },
            onWillStopSession() {
                treeView.clearModules();
            }
        }
    }

    private clearModules() {
        this.loadedModules = [];
        this.treeViewDataChangedEmitter.fire();
    }
}

class ModuleProperty {
    public readonly key: string;
    public readonly value: string;

    constructor(key: string, value: string) {
        this.key = key;
        this.value = value;
    }
}
