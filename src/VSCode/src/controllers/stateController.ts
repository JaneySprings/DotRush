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