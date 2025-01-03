import { QuickPickItem, workspace } from "vscode";
import * as path from "path";

export interface Project {
    name: string;
    path: string;
    frameworks: string[];
    configurations: string[];
    isTestProject: boolean;
    isExecutable: boolean;
}

export class ProjectOrSolutionItem implements QuickPickItem {
    label: string;
    description: string;
    item: string;

    constructor(targetPath: string) {
        this.description = workspace.asRelativePath(targetPath);
        this.item = targetPath;
        this.label = targetPath.endsWith('sln') 
            ? path.basename(targetPath, '.sln')
            : path.basename(targetPath, '.csproj');
    }
}

export class TargetFrameworkItem implements QuickPickItem {
    label: string;
    description: string;
    item: string;

    constructor(tfm: string, projectName: string) {
        this.label = projectName;
        this.description = tfm;
        this.item = tfm;
    }
}
