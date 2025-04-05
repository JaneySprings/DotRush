import { ExtensionContext } from 'vscode';
import * as res from '../resources/constants';

export class StateController {
    private static context: ExtensionContext | undefined;

    public static async activate(context: ExtensionContext): Promise<void> {
        StateController.context = context;
    }
    public static deactivate() {
        StateController.context = undefined;
    }

    public static getLocal<TValue>(key: string, defaultValue: TValue | undefined = undefined): TValue | undefined {
        return StateController.context?.workspaceState.get<TValue>(`${res.extensionId}.${key}`) ?? defaultValue;
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