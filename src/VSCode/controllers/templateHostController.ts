import { ChoiceInfo, TemplateInfo, TemplateInfoItem } from '../models/template';
import { Interop } from '../interop/interop';
import { Icons } from '../resources/icons';
import * as res from '../resources/constants';
import * as vscode from 'vscode';
import * as path from 'path';

export class TemplateHostController {
    public static activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdCreateNewProject, async () => {
            const items = await TemplateHostExtensions.getTemplateInfoGroups();
            if (items.length === 0)
                return;

            const templateItem = await vscode.window.showQuickPick(items, { placeHolder: res.messageSelectTemplateTitle, matchOnDescription: true, matchOnDetail: true });
            const template = (templateItem as TemplateInfoItem)?.item;
            if (template === undefined)
                return;

            const templateContext = await TemplateHostController.collectTemplateParameters(template);
            if (templateContext === undefined)
                return;

            const creationResult = await TemplateHostController.createProject(template, templateContext);
            if (creationResult)
                await TemplateHostController.processCreatedProject(templateContext.getProjectPath());
        }));
    }

    private static async collectTemplateParameters(template: TemplateInfo): Promise<TemplateContext | undefined> {
        const name = await TemplateHostExtensions.createStringCustomizer(res.messageNewProjectName);
        if (!name)
            return undefined;

        const result = new TemplateContext(name);
        for (const param of template.parameters ?? []) {
            if (param.type === 'string' || param.type === 'text') {
                const stringValue = await TemplateHostExtensions.createStringCustomizer(param.description ?? param.name);
                if (stringValue)
                    result.cliArguments[param.name] = stringValue;
            }
            else if (param.type === 'bool') {
                const boolValue = await TemplateHostExtensions.createBooleanCustomizer(param.description ?? param.name);
                if (boolValue !== undefined)
                    result.cliArguments[param.name] = boolValue ? "true" : "false";
            }
            else if (param.type === 'choice' && param.allowMultipleValues === true) {
                const choiceValues = await TemplateHostExtensions.createChoiceCustomizer(param.description ?? param.name, param.choices ?? {});
                if (choiceValues !== undefined && choiceValues.length > 0)
                    result.cliArguments[param.name] = choiceValues.join(";");
            }
            else if (param.type === 'choice' && param.allowMultipleValues === false) {
                const choiceValue = await TemplateHostExtensions.createSingleChoiceCustomizer(param.description ?? param.name, param.choices ?? {});
                if (choiceValue)
                    result.cliArguments[param.name] = choiceValue;
            }
        }

        result.directory = await TemplateHostExtensions.createDirectoryCustomizer();
        if (!result.directory)
            return undefined;

        return result;
    }
    private static async createProject(template: TemplateInfo, context: TemplateContext): Promise<boolean> {
        return vscode.window.withProgress({
            location: vscode.ProgressLocation.Notification,
            title: `Creating '${context.name}' ...`,
        }, async () => {
            const result = await Interop.createTemplate(template.identity, context.getProjectPath(), context.cliArguments);
            if (result === undefined || !result.isSuccess) {
                vscode.window.showErrorMessage(result?.message ?? "Project creation failed.");
                return false;
            }
            return true;
        });
    }
    private static async processCreatedProject(path: string): Promise<void> {
        if (vscode.workspace.workspaceFolders === undefined)
            return await vscode.commands.executeCommand('vscode.openFolder', vscode.Uri.file(path));

        const result = await vscode.window.showInformationMessage(res.messageNewProjectOpenAction, { modal: true }, res.messageAddToWorkspace, res.messageOpen);
        if (result === res.messageAddToWorkspace) {
            vscode.workspace.updateWorkspaceFolders(vscode.workspace.workspaceFolders.length, undefined, { uri: vscode.Uri.file(path) });
            return;
        }

        await vscode.commands.executeCommand('vscode.openFolder', vscode.Uri.file(path));
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
        const items = [{ label: `${Icons.yes} Yes`, value: true }, { label: `${Icons.no} No`, value: false }];
        const choice = await vscode.window.showQuickPick(items, { placeHolder: placeHolder, canPickMany: false, ignoreFocusOut: true });
        return choice?.value;
    }
    public static async createDirectoryCustomizer(): Promise<string | undefined> {
        const fileUri = await vscode.window.showOpenDialog({ canSelectMany: false, canSelectFolders: true, canSelectFiles: false });
        if (fileUri === undefined || fileUri.length === 0)
            return undefined;

        return fileUri[0].fsPath;
    }
}

class TemplateContext {
    name: string;
    directory?: string;
    cliArguments: { [key: string]: string } = {};

    constructor(name: string) {
        this.name = name;
    }

    public getProjectPath(): string {
        if (this.directory === undefined)
            return '';
        return path.join(this.directory, this.name);
    }
}