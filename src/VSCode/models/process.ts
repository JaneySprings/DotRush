import { QuickPickItem } from "vscode";
import { Icons } from "../resources/icons";

export interface Process {
    id: number;
    name: string;
    startTime: string;
}

export class ProcessItem implements QuickPickItem {
    label: string;
    description: string;
    item: Process;

    constructor(process: Process) {
        this.label = `${Icons.active} ${process.name} (${process.id})`;
        this.description = process.startTime;
        this.item = process;
    }
}
