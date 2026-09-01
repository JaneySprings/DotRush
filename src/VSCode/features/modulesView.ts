import { DebugAdapterController } from '../controllers/debugAdapterController';
import { Icons } from '../resources/icons';
import * as res from '../resources/constants';
import * as vscode from 'vscode';

export class ModulesView implements vscode.TreeDataProvider<ModuleTreeNode> {
    public static feature = new ModulesView();

    private loadedModules: DebugModule[] = [];
    private readonly treeViewDataEmitter = new vscode.EventEmitter<void>();

    public activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.window.registerTreeDataProvider(res.extendedViewIdModules, this));
        context.subscriptions.push(DebugAdapterController.tracker.onModuleLoaded((module: DebugModule) => {
            if (ModulesView.feature.loadedModules.some(it => it.id == module.id))
                return;

            ModulesView.feature.loadedModules.push(module);
            ModulesView.feature.treeViewDataEmitter.fire();
        }));
        // context.subscriptions.push(DebugAdapterController.tracker.onTargetExited(() => {
        //     ModulesView.feature.loadedModules = [];
        //     ModulesView.feature.treeViewDataEmitter.fire();
        // }));
    }

    onDidChangeTreeData = this.treeViewDataEmitter.event;
    getTreeItem(element: ModuleTreeNode): vscode.TreeItem {
        if ('value' in element) {
            const item = new vscode.TreeItem(element.name);
            item.description = element.value;
            item.tooltip = element.value;
            return item;
        }

        const item = new vscode.TreeItem(element.name, vscode.TreeItemCollapsibleState.Collapsed);
        item.iconPath = Icons.moduleIcon;
        return item;
    }
    getChildren(element?: ModuleTreeNode): vscode.ProviderResult<ModuleTreeNode[]> {
        if (element === undefined)
            return ModulesView.feature.loadedModules;
        if ('value' in element)
            return undefined;

        const properties = [];
        if (element.path)
            properties.push({ name: 'Path:', value: element.path });
        if (element.version)
            properties.push({ name: 'Version:', value: element.version });
        if (element.symbolStatus)
            properties.push({ name: 'Symbols:', value: element.symbolStatus });
        if (element.symbolFilePath)
            properties.push({ name: 'Symbol File:', value: element.symbolFilePath });

        properties.push({ name: 'Optimized:', value: element.isOptimized ? 'Yes' : 'No' });
        properties.push({ name: 'User Code:', value: element.isUserCode ? 'Yes' : 'No' });
        return properties;
    }
}

type ModuleTreeNode = DebugModule | ModuleProperty;
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
interface ModuleProperty {
    name: string;
    value: string;
}
