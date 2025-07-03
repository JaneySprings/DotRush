import { QuickPickItem, QuickPickItemKind, workspace } from "vscode";
import { Icons } from "../resources/icons";
import * as path from "path";

export interface Project {
    name: string;
    path: string;
    directory: string;
    frameworks: string[];
    configurations: string[];
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

export class ConfigurationItem implements QuickPickItem {
    label: string;
    description: string | undefined;
    configuration: string;
    framework: string | undefined;

    constructor(config: string, framework: string | undefined) {
        this.label = config;
        this.description = framework;
        this.configuration = config;
        this.framework = framework;
    }
}
