import { Range } from "vscode";

export interface TestCase {
    id: string;
    name: string;
    filePath: string;
    range: Range | null;
    children: TestCase[] | null;
}

export interface TestResult {
    fullName: string;
    duration: string;
    state: string;
    errorMessage: string | null;
}