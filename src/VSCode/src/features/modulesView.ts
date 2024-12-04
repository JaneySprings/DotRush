import { TreeDataProvider, DebugAdapterTracker, DebugAdapterTrackerFactory } from 'vscode';
import { Icons } from '../resources/icons';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

export class ModulesView implements TreeDataProvider<any>, DebugAdapterTrackerFactory {
    public static feature : ModulesView = new ModulesView();
    
    private loadedModules : any[] = [];
    private treeViewDataChangedEmitter = new vscode.EventEmitter();
    public readonly onDidChangeTreeData = this.treeViewDataChangedEmitter.event;

    public activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.window.registerTreeDataProvider(res.extendedViewIdModules, this));
        context.subscriptions.push(vscode.debug.registerDebugAdapterTrackerFactory(res.debuggerVsdbgId, this));
        context.subscriptions.push(vscode.debug.onDidStartDebugSession(() => this.treeViewDataChangedEmitter.fire(null), this));
    }

    public getChildren(element?: any): vscode.ProviderResult<any[]> {
        if (element == undefined)
            return this.loadedModules;
        
        if (!(element instanceof ModuleProperty)) {
            const props = [];
            if (element.path)
                props.push(new ModuleProperty('Path:', element.path));
            if (element.version)
                props.push(new ModuleProperty('Version:', element.version));
            if (element.symbolFilePath)
                props.push(new ModuleProperty('Symbols:', element.symbolFilePath));
            if (element.vsAppDomain)
                props.push(new ModuleProperty('App Domain:', element.vsAppDomain));
            if (element.addressRange)
                props.push(new ModuleProperty('Load Address:', element.addressRange));

            props.push(new ModuleProperty('Optimized:', element.isOptimized ? 'Yes' : 'No'));
            props.push(new ModuleProperty('User Code:', element.isUserCode ? 'Yes' : 'No'));

            return props;
        }

        return undefined;
    }
    public getTreeItem(element: any): vscode.TreeItem {
        if (element instanceof ModuleProperty) {
            const item = new vscode.TreeItem(element.key);
            item.description = element.value;
            item.tooltip = element.value;
            return item;
        } else {
            const item = new vscode.TreeItem(element.name, vscode.TreeItemCollapsibleState.Collapsed);
            item.iconPath = Icons.module;
            return item;
        }
    }
    public createDebugAdapterTracker(session: vscode.DebugSession): vscode.ProviderResult<DebugAdapterTracker> {
        const treeView = this;
        return {
            onDidSendMessage(message: any) {
                if (message.type != 'event' || message.event != 'module')
                    return;
                if (message.body.reason != 'new')
                    return;
        
                treeView.loadedModules.push(message.body.module);
                treeView.treeViewDataChangedEmitter.fire(null);
            },
            onWillStopSession() {
                treeView.loadedModules = [];
                treeView.treeViewDataChangedEmitter.fire(null);
            }
        }
    }
}

class ModuleProperty {
    key: string;
    value: string;

    constructor(key: string, value: string) {
        this.key = key;
        this.value = value;
    }
}
