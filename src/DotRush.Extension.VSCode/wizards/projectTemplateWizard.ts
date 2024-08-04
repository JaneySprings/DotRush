import { ProcessArgumentBuilder } from "../processes/processArgumentBuilder";
import { ProcessRunner } from "../processes/processRunner";
import { BaseTemplateWizard } from "./baseTemplateWizard";
import { Template } from "../models/template";
import * as res from "../resources/constants";
import * as vscode from 'vscode';
import * as path from 'path';

export class ProjectTemplateWizard extends BaseTemplateWizard {
    public static async invokeAsync() {
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

        await ProjectTemplateWizard.createTemplateAsync(template, templateName, templatePath);
        await ProjectTemplateWizard.finalizeTemplateAsync(vscode.Uri.file(templatePath));
    }

    private static async createTemplateAsync(template: Template, templateName: string, templatePath: string) { 
        await ProcessRunner.runAsync(new ProcessArgumentBuilder('dotnet')
            .append('new', template.invocation[0])
            .append('-o').appendQuoted(templatePath)
            .conditional(`-n "${templateName}"`, () => templateName !== ''));
    }

    private static async finalizeTemplateAsync(templatePath: vscode.Uri) {
        if (vscode.workspace.workspaceFolders === undefined)
            return await vscode.commands.executeCommand(res.taskCommandIdOpenFolder, templatePath);

        const result = await vscode.window.showInformationMessage(res.messageNewProjectOpenAction, { modal: true }, res.messageAddToWorkspace, res.messageOpen);
        if (result === res.messageAddToWorkspace)
            return vscode.workspace.updateWorkspaceFolders(vscode.workspace.workspaceFolders.length, undefined, { uri: templatePath });
        if (result === res.messageOpen)
            return await vscode.commands.executeCommand(res.taskCommandIdOpenFolder, templatePath);
    }
}