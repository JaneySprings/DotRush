import { Range, TestItem, TestController, Uri } from "vscode";

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

export class TestCaseExtensions {
    public static toTestItem(test: TestCase, controller: TestController): TestItem {
        const item = controller.createTestItem(test.id, test.name, Uri.file(test.filePath));
        if (test.range !== null)
            item.range = test.range;
        if (test.children !== null)
            item.children.replace(test.children.map(c => TestCaseExtensions.toTestItem(c, controller)));
        return item;
    }
}