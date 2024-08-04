import * as vscode from 'vscode';
import * as path from 'path';

export class ProjectItem implements vscode.QuickPickItem {
    label: string;
    description?: string;
    uri: vscode.Uri;

    constructor(projectPath: vscode.Uri) {
        this.label = path.basename(projectPath.fsPath);
        this.description = path.dirname(projectPath.fsPath);
        this.uri = projectPath;
    }
}