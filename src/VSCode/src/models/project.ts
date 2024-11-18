import { QuickPickItem } from "vscode";
import * as path from "path";

export interface Project {
    name: string;
    path: string;
    frameworks: string[];
    configurations: string[];
    isTestProject: boolean;
    isExecutable: boolean;
}

export class ProjectItem implements QuickPickItem {
    label: string;
    description: string;
    item: string;

    constructor(projectPath: string) {
        this.label = path.basename(projectPath, '.csproj');
        this.description = '';
        this.item = projectPath;
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
