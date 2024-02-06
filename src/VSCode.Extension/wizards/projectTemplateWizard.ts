import { ProcessArgumentBuilder } from "../processes/processArgumentBuilder";
import { ProcessRunner } from "../processes/processRunner";
import { BaseTemplateWizard } from "./baseTemplateWizard";
import * as res from "../resources/constants";
import * as vscode from 'vscode';
import * as path from 'path';

export class ProjectTemplateWizard extends BaseTemplateWizard {
    public static async createTemplateAsync() {
        const template = await ProjectTemplateWizard.selectTemplateAsync(res.messageSelectProjectTemplate, 'project', 'solution');
        if (template === undefined)
            return;

        const templateName = await ProjectTemplateWizard.getTemplateNameAsync(template.title, res.messageTemplateProjectNameInput);
        if (templateName === undefined)
            return;

        const targetPath = await ProjectTemplateWizard.getTemplatePathAsync(res.messageTemplateLocationLabel);
        if (targetPath === undefined)
            return;

        const templatePath = path.join(targetPath.fsPath, templateName);
        if (template.downloadLink !== undefined)
            await ProcessRunner.runAsync(new ProcessArgumentBuilder('dotnet').append('new', 'install', template.downloadLink));

        await ProcessRunner.runAsync(new ProcessArgumentBuilder('dotnet')
            .append('new', template.invocation[0])
            .append('-o').appendQuoted(templatePath)
            .conditional(`-n "${templateName}"`, () => templateName !== ''));
        await vscode.commands.executeCommand(res.taskCommandIdOpenFolder, vscode.Uri.file(templatePath));
    }
}