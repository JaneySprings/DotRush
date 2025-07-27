import { Range } from "vscode";

export interface TestItem {
    id: string;
    name: string;
    filePath: string;
    range: Range;
}

export interface TestResult {
    fullName: string;
    duration: string | null;
    state: string | null;
    stackTrace: string | null;
    errorMessage: string | null;
    children: TestItem[];
}
