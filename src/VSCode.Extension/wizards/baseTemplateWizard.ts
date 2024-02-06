import { DotNetTemplateProvider } from "../tasks/dotnetTemplateProvider";
import { TemplateItem } from "../models/templateItem";
import { Template } from "../models/template";
import * as vscode from 'vscode';

export class BaseTemplateWizard {
    protected static async selectTemplateAsync(placeHolder: string, ...filters: string[]): Promise<Template | undefined> {
        const templates = await DotNetTemplateProvider.getTemplates();
        const items = templates.filter(t => filters.includes(t.type)).map(t => new TemplateItem(t));
        if (items.length === 0)
            return undefined;

        const selectedItem = await vscode.window.showQuickPick(items, { 
            placeHolder: placeHolder,
            matchOnDetail: true,
        });
        return selectedItem?.item;
    }
    protected static async getTemplateNameAsync(title: string, placeHolder: string): Promise<string | undefined> {
        return await vscode.window.showInputBox({ placeHolder: placeHolder, title: title });
    }
    protected static async getTemplatePathAsync(openLabel: string): Promise<vscode.Uri | undefined> {
        const targetPath = await vscode.window.showOpenDialog({
            openLabel: openLabel,
            canSelectFiles: false,
            canSelectFolders: true,
            canSelectMany: false
        });
        return targetPath?.[0];
    }
}