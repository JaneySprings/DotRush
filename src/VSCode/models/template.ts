import { QuickPickItem } from "vscode";

export interface TemplateInfo {
    name: string;
    shortName: string;
    description: string;
    author: string;
    tags: string[];
}

export class TemplateInfoItem implements QuickPickItem {
    label: string;
    detail?: string;
    description?: string;
    item: TemplateInfo;

    constructor(info: TemplateInfo) {
        this.label = info.name;
        this.detail = info.description;
        this.description = info.tags?.join(",  ");
        this.item = info
    }
}