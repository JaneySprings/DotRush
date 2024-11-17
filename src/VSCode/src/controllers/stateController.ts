import { StatusBarController } from './statusbarController';
import { ExtensionContext } from 'vscode';
import * as res from '../resources/constants';

export class StateController {
    private static context: ExtensionContext | undefined;

    public static activate(context: ExtensionContext) {
        StateController.context = context;
    }
    public static deactivate() {
        StateController.context = undefined;
    }

    public static load() {
        if (StateController.context === undefined)
            return;

        const project = StateController.context.workspaceState.get<string>(`${res.extensionId}.project`);
        const configuration = StateController.context.workspaceState.get<string>(`${res.extensionId}.configuration`);

        StatusBarController.project = StatusBarController.projects.find(it => it.path === project);
        StatusBarController.configuration = StatusBarController.project?.configurations.find(it => it === configuration);
    }
    public static saveProject() {
        if (StateController.context !== undefined)
            StateController.context.workspaceState.update(`${res.extensionId}.project`, StatusBarController.project?.path);
    }
    public static saveConfiguration() {
        if (StateController.context !== undefined)
            StateController.context.workspaceState.update(`${res.extensionId}.configuration`, StatusBarController.configuration);
    }

    public static getGlobal<TValue>(key: string): TValue | undefined {
        return StateController.context?.globalState.get<TValue>(key);
    }
    public static putGlobal(key: string, value: any) {
        StateController.context?.globalState.update(key, value);
    }
}