import { ProcessArgumentBuilder } from "../processes/processArgumentBuilder";
import { ProcessRunner } from "../processes/processRunner";
import { BaseTemplateWizard } from "./baseTemplateWizard";
import { Template } from "../models/template";
import * as res from "../resources/constants";
import * as vscode from 'vscode';

export class ItemTemplateWizard extends BaseTemplateWizard {
    public static async invokeAsync(targetPath: vscode.Uri) {
        const template = await ItemTemplateWizard.selectTemplateAsync(res.messageSelectItemTemplate, 'item');
        if (template === undefined)
            return;

        const templateName = await ItemTemplateWizard.getTemplateNameAsync(template.title, res.messageTemplateItemNameInput);
        if (templateName === undefined)
            return;

        await ItemTemplateWizard.createTemplateAsync(template, templateName, targetPath.fsPath);
    }

    private static async createTemplateAsync(template: Template, templateName: string, targetPath: string) {
        await ProcessRunner.runAsync(new ProcessArgumentBuilder('dotnet')
            .append('new', template.invocation[0])
            .append('-o').appendQuoted(targetPath)
            .conditional(`-n "${templateName}"`, () => templateName !== '')
        );
    }
}