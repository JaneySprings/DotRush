import { ProcessArgumentBuilder } from "../processes/processArgumentBuilder";
import { DotNetTemplateProvider } from "../tasks/dotnetTemplateProvider";
import { ProcessRunner } from "../processes/processRunner";
import { TemplateItem } from "../models/templateItem";
import { Template } from "../models/template";
import * as res from "../resources/constants";
import * as vscode from 'vscode';

export class ItemTemplateWizard {
    public static async createTemplateAsync(targetPath: vscode.Uri) {
        const template = await this.selectTemplateAsync();
        if (!template)
            return;

        const templateName = await this.getTemplateNameAsync(template.title);
        if (templateName === undefined)
            return;

        await ProcessRunner.runAsync(new ProcessArgumentBuilder('dotnet')
            .append('new', template.invocation[0])
            .append('-o').appendQuoted(targetPath.fsPath)
            .conditional(`-n "${templateName}"`, () => templateName !== '')
        );
    }

    private static async selectTemplateAsync(): Promise<Template | undefined> {
        const templates = await DotNetTemplateProvider.getTemplates();
        const items = templates.filter(t => t.type === "item").map(t => new TemplateItem(t));
        if (items.length === 0)
            return undefined;

        const selectedItem = await vscode.window.showQuickPick(items, {
            placeHolder: res.messageTemplateSelect
        });
        return selectedItem?.item;
    }
    private static async getTemplateNameAsync(title: string | undefined): Promise<string | undefined> {
        return await vscode.window.showInputBox({
            placeHolder: res.messageTemplateNameInput,
            title: title
        });
    }
}