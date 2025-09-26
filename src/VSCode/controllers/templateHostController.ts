import { TemplateInfoItem } from '../models/template';
import { Interop } from '../interop/interop';
import * as vscode from 'vscode';
import * as res from '../resources/constants';

export class TemplateHostController {
    public static activate(context: vscode.ExtensionContext) {
        context.subscriptions.push(vscode.commands.registerCommand(res.commandIdCreateNewProject, async () => {
            const items = await TemplateHostExtensions.getTemplateInfoGroups();
            const template = await vscode.window.showQuickPick(items, { placeHolder: res.messageSelectTemplateTitle, matchOnDescription: true, matchOnDetail: true });
            if (template === undefined)
                return;


        }));
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
}