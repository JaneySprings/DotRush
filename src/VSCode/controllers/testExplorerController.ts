import { ProcessArgumentBuilder } from '../interop/processArgumentBuilder';
import { LanguageServerController } from './languageServerController';
import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { TestItem, TestResult } from '../models/test';
import { Extensions } from '../extensions';
import { Project } from '../models/project';
import { Icons } from '../resources/icons';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants'
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export class TestExplorerController {
    private static controller: vscode.TestController;

    public static activate(context: vscode.ExtensionContext) {
        TestExplorerController.controller = vscode.tests.createTestController(res.testExplorerViewId, res.testExplorerViewTitle);
        TestExplorerController.controller.refreshHandler = TestExplorerController.refreshTestItems;
        TestExplorerController.controller.resolveHandler = TestExplorerController.resolveTestItem;

        context.subscriptions.push(TestExplorerController.controller);
        context.subscriptions.push(TestExplorerController.controller.createRunProfile(res.testExplorerProfileRun, vscode.TestRunProfileKind.Run, TestExplorerController.runTests, true));
        context.subscriptions.push(TestExplorerController.controller.createRunProfile(res.testExplorerProfileDebug, vscode.TestRunProfileKind.Debug, TestExplorerController.debugTests, true));

        context.subscriptions.push(vscode.workspace.onDidOpenTextDocument(async ev => await TestExplorerController.loadFixtures(ev.uri.fsPath)));
        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async ev => await TestExplorerController.loadFixtures(ev.uri.fsPath)));
        //TODO: TestHost
        TestExplorerController.testsResultDirectory = path.join(context.extensionPath, "extension", "bin", "TestExplorer");
        if (Extensions.getSetting(res.configIdTestExplorerSkipInitialPauseEvent) && Extensions.onVSCode(true, false)) {
            context.subscriptions.push(vscode.debug.registerDebugAdapterTrackerFactory(res.debuggerNetCoreId, new ContinueAfterInitialPauseTracker()));
        }
    }
    public static loadProject(project: Project): void {
        const item = TestExplorerExtensions.createProjectItem(TestExplorerController.controller, project);
        TestExplorerController.controller.items.add(item);
    }
    public static async loadFixtures(itemPath: string): Promise<void> {
        const project = TestExplorerExtensions.findProjectItem(itemPath, TestExplorerController.controller.items);
        if (project === undefined)
            return;

        await TestExplorerController.resolveTestItem(project);
        project.children.forEach(fixture => {
            if (fixture.tags.find(t => t.id === itemPath) !== undefined)
                TestExplorerController.resolveTestItem(fixture);
        });
    }

    private static refreshTestItems() {
        TestExplorerController.controller.items.forEach(TestExplorerController.resolveTestItem);
    }
    private static async resolveTestItem(item?: vscode.TestItem): Promise<void> {
        if (item === undefined || !item.canResolveChildren)
            return;

        if (TestExplorerExtensions.isProjectItem(item)) {
            const fixtures = await LanguageServerController.sendRequest<TestItem[]>('dotrush/testExplorer/fixtures', { textDocument: Extensions.documentIdFromUri(item.uri) });
            if (fixtures !== undefined && fixtures.length > 0)
                item.children.replace(fixtures.map(fixture => TestExplorerExtensions.createTestItem(TestExplorerController.controller, fixture, true)));
        }
        else if (TestExplorerExtensions.isFixtureItem(item)) {
            const testCases = await LanguageServerController.sendRequest<TestItem[]>('dotrush/testExplorer/tests', { textDocument: Extensions.documentIdFromUri(item.uri), fixtureId: item.id });
            if (testCases !== undefined && testCases.length > 0)
                item.children.replace(testCases.map(testCase => TestExplorerExtensions.createTestItem(TestExplorerController.controller, testCase, false, item.id)));
        }
    }

    //TODO: TestHost
    private static testsResultDirectory: string;
    private static async runTests(request: vscode.TestRunRequest, token: vscode.CancellationToken): Promise<void> {
        TestExplorerController.convertTestRequest(request).forEach(async (filters, project) => {
            const preLaunchTask = await Extensions.getTask(Extensions.getSetting<string>(res.configIdTestExplorerPreLaunchTask));
            const testReport = path.join(TestExplorerController.testsResultDirectory, `${project.id}.trx`);
            if (fs.existsSync(testReport))
                vscode.workspace.fs.delete(vscode.Uri.file(testReport));

            const testArguments = new ProcessArgumentBuilder('test')
                .append('--logger').append(`'trx;LogFileName=${testReport}'`);

            testArguments.conditional('--no-build', () => preLaunchTask !== undefined);
            testArguments.conditional('--filter', () => filters.length > 0);
            testArguments.conditional(`'${filters.join('|')}'`, () => filters.length > 0);

            const testRun = TestExplorerController.controller.createTestRun(request);
            if (preLaunchTask !== undefined) {
                const executionSuccess = await Extensions.waitForTask(preLaunchTask);
                if (!executionSuccess || token.isCancellationRequested)
                    return;
            }
            await Extensions.waitForTask(DotNetTaskProvider.getTestTask(project.uri!.fsPath, testArguments));
            await TestExplorerController.publishTestResults(testRun, project, testReport);
        });
    }
    private static async debugTests(request: vscode.TestRunRequest, token: vscode.CancellationToken): Promise<void> {
        TestExplorerController.convertTestRequest(request).forEach(async (filters, project) => {
            const preLaunchTask = await Extensions.getTask(Extensions.getSetting<string>(res.configIdTestExplorerPreLaunchTask));
            const executionSuccess = await Extensions.waitForTask(preLaunchTask ?? DotNetTaskProvider.getBuildTask(project.uri!.fsPath));
            if (!executionSuccess || token.isCancellationRequested)
                return;

            const processId = await Interop.runTestHost(project.uri!.fsPath, filters.join('|'));
            await vscode.debug.startDebugging(Extensions.getWorkspaceFolder(), {
                name: res.testExplorerProfileDebug,
                type: res.debuggerNetCoreId,
                processId: processId,
                request: 'attach',
            });
        });
    }
    private static convertTestRequest(request: vscode.TestRunRequest): Map<vscode.TestItem, string[]> {
        const testItems = new Map<vscode.TestItem, string[]>();
        const getRootNode = (item: vscode.TestItem): vscode.TestItem => {
            if (item.parent === undefined)
                return item;
            return getRootNode(item.parent);
        }

        if (request.include === undefined) {
            TestExplorerController.controller.items.forEach(item => testItems.set(item, []));
            return testItems;
        }

        request.include?.forEach(item => {
            const rootNode = getRootNode(item);
            if (!testItems.has(rootNode))
                testItems.set(rootNode, []);
            if (item.id !== rootNode.id)
                testItems.get(rootNode)?.push(item.id);
        });

        return testItems;
    }
    private static publishTestResults(testRun: vscode.TestRun, project: vscode.TestItem, testReport: string): Promise<void> {
        const findTestItem = (id: string) => {
            const fixtureId = id.substring(0, id.lastIndexOf('.'));
            const fixture = project.children.get(fixtureId);
            if (fixture === undefined)
                return undefined;

            return fixture.children.get(id);
        }

        return Interop.getTestResults(testReport).then(testResults => {
            testResults?.forEach(result => {
                const duration = TestExplorerExtensions.toDurationNumber(result.duration);
                const testItem = findTestItem(result.fullName);
                if (testItem === undefined)
                    return;

                if (result.state === 'Passed')
                    testRun.passed(testItem, duration);
                else if (result.state === 'Failed')
                    testRun.failed(testItem, TestExplorerExtensions.toTestMessage(result), duration);
                else
                    testRun.skipped(testItem);
            });
            testRun.end();
        });
    }
}

class TestExplorerExtensions {
    public static createProjectItem(controller: vscode.TestController, project: Project): vscode.TestItem {
        const item = controller.createTestItem(project.name, `${Icons.library} ${project.name}`, vscode.Uri.file(project.path));
        item.canResolveChildren = true;
        return item;
    }
    public static createTestItem(controller: vscode.TestController, modelItem: TestItem, canResolve: boolean, parentId?: string): vscode.TestItem {
        const id = parentId ? `${parentId}.${modelItem.id}` : modelItem.id;
        const item = controller.createTestItem(id, modelItem.name, vscode.Uri.file(modelItem.filePath));
        item.range = modelItem.range;
        item.tags = modelItem.locations.map(location => new vscode.TestTag(location));
        item.canResolveChildren = canResolve;
        return item;
    }

    public static isProjectItem(item: vscode.TestItem): boolean {
        return item.parent === undefined;
    }
    public static isFixtureItem(item: vscode.TestItem): boolean {
        return item.parent !== undefined && item.parent.parent === undefined;
    }
    public static isTestCaseItem(item: vscode.TestItem): boolean {
        return item.parent !== undefined && item.parent.parent !== undefined;
    }

    public static findProjectItem(childPath: string, items: vscode.TestItemCollection): vscode.TestItem | undefined {
        if (!childPath.endsWith('.cs'))
            return undefined;

        for (const item of items) {
            if (item[1].uri === undefined)
                continue;

            const itemDirectory = path.dirname(item[1].uri.fsPath);
            if (childPath.startsWith(itemDirectory))
                return item[1];
        }
        return undefined;
    }

    //TODO: TestHost
    public static toTestMessage(testResult: TestResult): vscode.TestMessage {
        let message = testResult.errorMessage ?? '';
        if (testResult.stackTrace !== null)
            message += `\n\n${testResult.stackTrace}`;
        return new vscode.TestMessage(message);
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

// https://github.com/microsoft/vstest/blob/06101ef5feb95048cbe850472ed49604863d54ff/src/vstest.console/Program.cs#L37
class ContinueAfterInitialPauseTracker implements vscode.DebugAdapterTrackerFactory {
    private isInitialStoppedEvent: boolean = false;

    public createDebugAdapterTracker(session: vscode.DebugSession): vscode.ProviderResult<vscode.DebugAdapterTracker> {
        const self = this;
        return {
            onDidSendMessage(message: any) {
                if (session.name !== res.testExplorerProfileDebug)
                    return;

                if (message.type == 'response' && message.command == 'initialize')
                    self.isInitialStoppedEvent = true;
                if (message.type == 'event' && message.event == 'stopped' && self.isInitialStoppedEvent) {
                    session.customRequest('continue', { threadId: message.body.threadId });
                    self.isInitialStoppedEvent = false;
                }
            }
        }
    }
}