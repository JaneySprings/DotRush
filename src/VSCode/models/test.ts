import { Range, TestItem, TestController, Uri, TestMessage } from "vscode";
import { Icons } from "../resources/icons";

export interface TestFixture {
    id: string;
    name: string;
    filePath: string;
    range: Range | null;
    children: TestCase[];
}

export interface TestCase {
    id: string;
    name: string;
    filePath: string;
    range: Range | null;
}

export interface TestResult {
    fullName: string;
    duration: string | null;
    state: string | null;
    stackTrace: string | null;
    errorMessage: string | null;
}

export class TestExtensions {
    public static fixtureToTestItem(fixture: TestFixture, controller: TestController): TestItem {
        const item = controller.createTestItem(fixture.id, `${Icons.module} ${fixture.name}`, Uri.file(fixture.filePath));
        if (fixture.range !== null)
            item.range = fixture.range;
        if (fixture.children !== null && fixture.children !== undefined)
            item.children.replace(fixture.children.map(tc => TestExtensions.testCaseToTestItem(tc, controller)));
        return item;
    }
    public static testCaseToTestItem(testCase: TestCase, controller: TestController): TestItem {
        const item = controller.createTestItem(testCase.id, `${Icons.test} ${testCase.name}`, Uri.file(testCase.filePath));
        if (testCase.range !== null)
            item.range = testCase.range;
        return item;
    }
    public static toTestMessage(testResult: TestResult): TestMessage {
        let message = testResult.errorMessage ?? '';
        if (testResult.stackTrace !== null)
            message += `\n\n${testResult.stackTrace}`;
        return new TestMessage(message);
    }
    public static toDurationNumber(duration: string | null): number {
        const match = duration?.match(/(\d+):(\d+):(\d+)\.(\d+)/);
        if (duration === null || !match)
            return 1;

        const [_, hours, minutes, seconds, milliseconds] = match;
        return parseInt(hours, 10) * 60 * 60 * 1000 +
            parseInt(minutes, 10) * 60 * 1000 +
            parseInt(seconds, 10) * 1000 +
            parseInt(milliseconds.slice(0, 3), 10);
    }
}