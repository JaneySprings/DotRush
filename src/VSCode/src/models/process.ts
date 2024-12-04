import { QuickPickItem } from "vscode";

export interface Process {
    id: number;
    name: string;
}

export class ProcessItem implements QuickPickItem {
    label: string;
    description: string;
    item: Process;

    constructor(process: Process) {
        this.label = process.name;
        this.description = process.id.toString();
        this.item = process;
    }
}
