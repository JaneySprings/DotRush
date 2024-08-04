import { Template } from "./template";
import { QuickPickItem } from "vscode";

export class TemplateItem implements QuickPickItem {
    label: string;
    detail?: string;
    item: Template;

    constructor(template: Template) {
        this.label = template.title;
        this.detail = template.tags?.join(', ');
        this.item = template;
    }
}
