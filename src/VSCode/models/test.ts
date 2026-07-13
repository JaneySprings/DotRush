import { Range } from "vscode";

export interface TestItem {
    id: string;
    name: string;
    filePath: string;
    range: Range;
    locations: string[];
    children?: TestItem[]
}

export enum Outcome {
    None = 0,
    Passed = 1,
    Failed = 2,
    Skipped = 3,
    NotFound = 4,
}