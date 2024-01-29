import { Template } from "./template";
import { QuickPickItem } from "vscode";

export class TemplateItem implements QuickPickItem {
    label: string;
    item: Template;

    constructor(template: Template) {
        this.label = template.title;
        this.item = template;
    }
}
