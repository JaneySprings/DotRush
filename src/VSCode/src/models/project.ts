import { QuickPickItem } from "vscode";
import * as path from "path";

export interface Project {
    name: string;
    path: string;
    frameworks: string[];
    configurations: string[];
    isTestProject: boolean;
}

export class ProjectItem implements QuickPickItem {
    label: string;
    description: string;
    detail: string;
    item: Project;

    constructor(project: Project) {
        this.label = project.name;
        this.detail = project.path;
        this.description = project.frameworks?.join('  ');
        this.item = project;
    }

    public static from(projectPath: string): ProjectItem {
        return new ProjectItem({
            name: path.basename(projectPath, '.csproj'),
            path: projectPath,
            frameworks: [],
            configurations: [],
            isTestProject: false
        });
    }
}
