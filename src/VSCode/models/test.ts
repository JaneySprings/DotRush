import { Range, TestItem, TestController, Uri, TestMessage } from "vscode";
import { Icons } from "../resources/icons";

export interface TestFixture {
    id: string;
    name: string;
    filePath: string;
    range: Range;
    children: TestCase[];
}

export interface TestCase {
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
}