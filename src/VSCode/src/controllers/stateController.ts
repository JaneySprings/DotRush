import { ExtensionContext } from 'vscode';
import * as res from '../resources/constants';
import { StatusBarController } from './statusbarController';

export class StateController {
    private static context: ExtensionContext | undefined;

    public static async activate(context: ExtensionContext): Promise<void> {
        StateController.context = context;
    }
    public static deactivate() {
        StateController.context = undefined;
    }

    public static putProject(path: string | undefined) {
        StateController.putLocal('project', path);
    }
    public static putFramework(framework: string | undefined) {
        StateController.putLocal('framework', framework);
    }
    public static putConfiguration(configuration: string | undefined) {
        StateController.putLocal('configuration', configuration);
    }

    public static getProject() : string | undefined {
        const projectPath = StateController.getLocal<string>('project');
        return StatusBarController.projects?.find(it => it === projectPath);
    }
    public static getFramework() : string | undefined {
        return StateController.getLocal<string>('framework');
    }
    public static getConfiguration() : string | undefined {
        return StateController.getLocal<string>('configuration');
    }


    public static getLocal<TValue>(key: string): TValue | undefined {
        return StateController.context?.workspaceState.get<TValue>(`${res.extensionId}.${key}`);
    }
    public static putLocal(key: string, value: any) {
        StateController.context?.workspaceState.update(`${res.extensionId}.${key}`, value);
    }

    public static getGlobal<TValue>(key: string): TValue | undefined {
        return StateController.context?.globalState.get<TValue>(key);
    }
    public static putGlobal(key: string, value: any) {
        StateController.context?.globalState.update(key, value);
    }
}