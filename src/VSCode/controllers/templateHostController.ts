import { ChoiceInfo, TemplateInfo, TemplateInfoItem } from '../models/template';
import { Interop } from '../interop/interop';
import { Icons } from '../resources/icons';
import * as vscode from 'vscode';
import * as res from '../resources/constants';

export class TemplateHostController {
    public static activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdCreateNewProject, async () => {
            const items = await TemplateHostExtensions.getTemplateInfoGroups();
            const template = await vscode.window.showQuickPick(items, { placeHolder: res.messageSelectTemplateTitle, matchOnDescription: true, matchOnDetail: true });
            if (template === undefined)
                return;

            const parameters = await TemplateHostController.collectTemplateParameters((template as TemplateInfoItem).item);
            if (parameters === undefined)
                return;
        }));
    }

    private static async collectTemplateParameters(template: TemplateInfo): Promise<TemplateParameters | undefined> {
        const result = new TemplateParameters();
        result.name = await TemplateHostExtensions.createStringCustomizer("Project Name");
        if (result.name === undefined)
            return undefined;
        result.directory = await TemplateHostExtensions.createDirectoryCustomizer();
        if (result.directory === undefined)
            return undefined;

        for (const param of template.parameters ?? []) {
            if (param.type === 'string' || param.type === 'text') {
                const stringValue = await TemplateHostExtensions.createStringCustomizer(param.description ?? param.name);
                if (stringValue !== undefined)
                    result.cliArguments.push(`--${param.name}`, stringValue);
            }
            else if (param.type === 'bool') {
                const boolValue = await TemplateHostExtensions.createBooleanCustomizer(param.description ?? param.name);
                if (boolValue !== undefined)
                    result.cliArguments.push(`--${param.name}`, boolValue ? "true" : "false");
            }
            else if (param.type === 'choice' && param.allowMultipleValues === true) {
                const choiceValues = await TemplateHostExtensions.createChoiceCustomizer(param.description ?? param.name, param.choices ?? {});
                if (choiceValues !== undefined && choiceValues.length > 0)
                    result.cliArguments.push(`--${param.name}`, choiceValues.join(";"));

            }
            else if (param.type === 'choice' && param.allowMultipleValues === false) {
                const choiceValue = await TemplateHostExtensions.createSingleChoiceCustomizer(param.description ?? param.name, param.choices ?? {});
                if (choiceValue !== undefined)
                    result.cliArguments.push(`--${param.name}`, choiceValue);
            }
        }

        return result;
    }
}

export class TemplateHostExtensions {
    public static async getTemplateInfoGroups(): Promise<vscode.QuickPickItem[]> {
        const templates = await Interop.getTemplates();
        if (templates === undefined || templates.length === 0)
            return [];

        const templatesMap = new Map<string, TemplateInfoItem[]>();
        for (const info of templates) {
            const author = info.author ?? "Unknown";
            if (!templatesMap.has(author))
                templatesMap.set(author, []);
            templatesMap.get(author)?.push(new TemplateInfoItem(info));
        }

        const items: vscode.QuickPickItem[] = [];
        for (const [author, infos] of templatesMap) {
            items.push({ label: author, kind: vscode.QuickPickItemKind.Separator });
            items.push(...infos);
        }
        return items;
    }

    public static async createStringCustomizer(placeholder: string): Promise<string | undefined> {
        return await vscode.window.showInputBox({ placeHolder: placeholder, ignoreFocusOut: true });
    }
    public static async createSingleChoiceCustomizer(placeHolder: string, choices: { [key: string]: ChoiceInfo }): Promise<string | undefined> {
        const items = Object.entries(choices).map(([key, choice]) => {
            return {
                label: choice.description ?? choice.name ?? key,
                value: key
            };
        });

        const choice = await vscode.window.showQuickPick(items, { placeHolder: placeHolder, canPickMany: false, ignoreFocusOut: true });
        return choice?.value;
    }
    public static async createChoiceCustomizer(placeHolder: string, choices: { [key: string]: ChoiceInfo }): Promise<string[] | undefined> {
        const items = Object.entries(choices).map(([key, choice]) => {
            return {
                label: choice.description ?? choice.name ?? key,
                value: key
            };
        });

        const selected = await vscode.window.showQuickPick(items, { placeHolder: placeHolder, canPickMany: true, ignoreFocusOut: true });
        return selected?.map(s => s.value);
    }
    public static async createBooleanCustomizer(placeHolder: string): Promise<boolean | undefined> {
        const choice = await vscode.window.showQuickPick([`${Icons.yes} Yes`, `${Icons.no} No`], { placeHolder: placeHolder, canPickMany: false, ignoreFocusOut: true });
        if (choice === undefined)
            return undefined;
        return choice === "Yes";
    }
    public static async createDirectoryCustomizer(): Promise<string | undefined> {
        const fileUri = await vscode.window.showOpenDialog({ canSelectMany: false, canSelectFolders: true, canSelectFiles: false });
        if (fileUri === undefined || fileUri.length === 0)
            return undefined;

        return fileUri[0].fsPath;
    }
}

class TemplateParameters {
    name?: string;
    directory?: string;
    cliArguments: string[] = [];
}