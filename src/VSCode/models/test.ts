import { Range, TestItem, TestController, Uri, TestMessage } from "vscode";

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
    stackTrace: string | null;
    errorMessage: string | null;
}

export class TestExtensions {
    public static toTestItem(test: TestCase, controller: TestController): TestItem {
        const item = controller.createTestItem(test.id, test.name, Uri.file(test.filePath));
        if (test.range !== null)
            item.range = test.range;
        if (test.children !== null)
            item.children.replace(test.children.map(c => TestExtensions.toTestItem(c, controller)));
        return item;
    }
    public static toTestMessage(testResult: TestResult): TestMessage {
        let message = testResult.errorMessage ?? '';
        if (testResult.stackTrace !== null)
            message += `\n\n${testResult.stackTrace}`;
        return new TestMessage(message);
    }
}