import { QuickPickItem, QuickPickItemKind, workspace } from "vscode";
import * as path from "path";
import { Icons } from "../resources/icons";

export interface Project {
    name: string;
    path: string;
    frameworks: string[];
    configurations: string[];
    isTestProject: boolean;
    isExecutable: boolean;
}

export class ProjectOrSolutionItem implements QuickPickItem {
    public static solutionSeparator: QuickPickItem = {
        label: 'Solutions',
        kind: QuickPickItemKind.Separator
    };
    public static projectSeparator: QuickPickItem = {
        label: 'Projects',
        kind: QuickPickItemKind.Separator
    };

    label: string;
    description: string;
    item: string;

    constructor(targetPath: string) {
        this.description = workspace.asRelativePath(targetPath);
        this.item = targetPath;

        const icon = path.extname(targetPath).startsWith('.sln') ? Icons.solution : Icons.project;
        const name = path.basename(targetPath, path.extname(targetPath));
        this.label = `${icon} ${name}`;
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
