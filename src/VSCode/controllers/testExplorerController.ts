import { LanguageServerController } from './languageServerController';
import { DebugAdapterController } from './debugAdapterController';
import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { Outcome, TestItem } from '../models/test';
import { Extensions } from '../extensions';
import { Project } from '../models/project';
import { Icons } from '../resources/icons';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants'
import * as vscode from 'vscode';
import * as path from 'path';

export class TestExplorerController {
    private static controller: vscode.TestController;

    public static activate(context: vscode.ExtensionContext) {
        TestExplorerController.controller = vscode.tests.createTestController(res.testExplorerViewId, res.testExplorerViewTitle);
        TestExplorerController.controller.refreshHandler = TestExplorerController.refreshTestItems;
        TestExplorerController.controller.resolveHandler = TestExplorerController.resolveTestItem;

        context.subscriptions.push(TestExplorerController.controller);
        context.subscriptions.push(TestExplorerController.controller.createRunProfile(res.testExplorerProfileRun, vscode.TestRunProfileKind.Run, async (r, ct) => await TestExplorerController.createTestRun(r, false, ct), true));
        context.subscriptions.push(TestExplorerController.controller.createRunProfile(res.testExplorerProfileDebug, vscode.TestRunProfileKind.Debug, async (r, ct) => await TestExplorerController.createTestRun(r, true, ct), true));

        context.subscriptions.push(vscode.workspace.onDidOpenTextDocument(async ev => await TestExplorerController.resolveTestItemsByPath(ev.uri.fsPath)));
        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async ev => await TestExplorerController.resolveTestItemsByPath(ev.uri.fsPath)));
    }
    public static loadProject(project: Project): void {
        const item = TestExplorerExtensions.createProjectItem(TestExplorerController.controller, project);
        TestExplorerController.controller.items.add(item);
    }
    public static async resolveTestItemsByPath(documentPath: string): Promise<void> {
        const project = TestExplorerExtensions.findProjectItem(documentPath, TestExplorerController.controller.items);
        if (project === undefined)
            return;

        if (project.children.size === 0)
            return TestExplorerController.resolveTestItem(project);

        var isProcessed = false;
        project.children.forEach(fixture => {
            if (fixture.tags.find(t => t.id === documentPath) !== undefined) {
                TestExplorerController.resolveTestItem(fixture);
                isProcessed = true;
            }
        });
        if (!isProcessed) {
            TestExplorerController.resolveNewDocument(documentPath);
        }
    }

    private static refreshTestItems() {
        TestExplorerController.controller.items.forEach(TestExplorerController.resolveTestItem);
    }
    private static async resolveTestItem(item?: vscode.TestItem): Promise<void> {
        if (item === undefined || !item.canResolveChildren)
            return;

        if (TestExplorerExtensions.isProjectItem(item)) {
            const fixtures = await LanguageServerController.sendRequest<TestItem[]>('dotrush/testExplorer/fixtures', { textDocument: Extensions.documentIdFromUri(item.uri) });
            if (fixtures !== undefined)
                item.children.replace(fixtures.map(fixture => TestExplorerExtensions.createTestItem(TestExplorerController.controller, fixture, true)));

            for (const child of item.children)
                await TestExplorerController.resolveTestItem(child[1]);
        }
        else if (TestExplorerExtensions.isFixtureItem(item)) {
            const testCases = await LanguageServerController.sendRequest<TestItem[]>('dotrush/testExplorer/tests', { textDocument: Extensions.documentIdFromUri(item.uri), fixtureId: item.id });
            if (testCases !== undefined)
                item.children.replace(testCases.map(testCase => TestExplorerExtensions.createTestItem(TestExplorerController.controller, testCase, false, item.id)));
        }
    }
    private static async resolveNewDocument(documentPath: string): Promise<void> {
        const project = TestExplorerExtensions.findProjectItem(documentPath, TestExplorerController.controller.items);
        if (project === undefined)
            return;
        const documentId = Extensions.documentIdFromUri(vscode.Uri.file(documentPath));
        const fixtures = await LanguageServerController.sendRequest<TestItem[]>('dotrush/testExplorer/fixtures', { textDocument: documentId });
        if (fixtures !== undefined)
            project.children.replace(fixtures.map(fixture => TestExplorerExtensions.createTestItem(TestExplorerController.controller, fixture, true)));
    }
    private static async createTestRun(request: vscode.TestRunRequest, attachDebugger: boolean, token: vscode.CancellationToken): Promise<void> {
        return TestExplorerExtensions.convertTestRequest(request, async (projects, filter) => {
            // Initialize projects
            if (projects.length === 0)
                TestExplorerController.controller.items.forEach(item => projects.push(item));
            for (const project of projects) {
                if (project.children.size === 0)
                    await TestExplorerController.resolveTestItem(project);
            }

            // Build projects
            const preLaunchTask = await Extensions.getTask(Extensions.getSetting<string>(res.configIdTestExplorerPreLaunchTask));
            if (preLaunchTask !== undefined) {
                const executionSuccess = await Extensions.waitForTask(preLaunchTask);
                if (!executionSuccess || token.isCancellationRequested)
                    return;
            }
            else for (const project of projects) {
                const preLaunchTask = DotNetTaskProvider.getBuildTask(project.uri!.fsPath);
                const executionSuccess = await Extensions.waitForTask(preLaunchTask);
                if (!executionSuccess || token.isCancellationRequested)
                    return;
            }
            vscode.commands.executeCommand('workbench.panel.testResults.view.focus');

            // Collect test assemblies
            const testAssemblies: string[] = [];
            for (const project of projects) {
                const targetPath = await DebugAdapterController.getProjectTargetPath(project.uri!.fsPath);
                if (targetPath != undefined)
                    testAssemblies.push(targetPath);
            }
            if (testAssemblies.length === 0 || token.isCancellationRequested)
                return;

            // Run tests
            const testRun = TestExplorerController.controller.createTestRun(request);
            const testHostRpc = Interop.createTestHostRpc(builder => builder
                .append(...testAssemblies.map(assembly => `-a:"${assembly}"`))
                .conditional2(() => filter.length > 0, ...filter.map(f => `-f:${f}`))
                .conditional('-d', () => attachDebugger));

            testHostRpc.onRequest('attachDebuggerToProcess', async (processId: number) => {
                return await vscode.debug.startDebugging(Extensions.getWorkspaceFolder(), {
                    name: res.testExplorerProfileDebug,
                    type: res.debuggerNetCoreId,
                    processId: processId,
                    request: 'attach',
                });
            });
            testHostRpc.onNotification('handleMessage', (data: string) => testRun.appendOutput(`${data.trimEnd()}\r\n`));
            testHostRpc.onNotification('handleTestRunStatsChange', (data: any) => data?.NewTestResults?.forEach((result: any) => {
                testRun.appendOutput(`[${TestExplorerExtensions.toTestStatus(result.Outcome)}]: ${result.DisplayName}\r\n`);

                function findTestItem(id: string): vscode.TestItem | undefined {
                    if (id.includes('('))
                        id = id.substring(0, id.indexOf('('));
                    const fixtureId = id.substring(0, id.lastIndexOf('.'));
                    for (const project of projects) {
                        const fixture = project.children.get(fixtureId);
                        if (fixture !== undefined)
                            return fixture.children.get(id);
                    }
                    return undefined;
                };

                const testItem = findTestItem(result.TestCase.FullyQualifiedName);
                if (testItem === undefined)
                    return;

                if (result.Outcome === Outcome.Passed)
                    testRun.passed(testItem, TestExplorerExtensions.toDurationNumber(result.Duration));
                else if (result.Outcome === Outcome.Failed)
                    testRun.failed(testItem, TestExplorerExtensions.toTestMessage(result.ErrorMessage, result.ErrorStackTrace), TestExplorerExtensions.toDurationNumber(result.Duration));
                else if (result.Outcome === Outcome.Skipped)
                    testRun.skipped(testItem);
            }));
            testHostRpc.onNotification('handleTestRunComplete', (_) => {
                testRun.end();
                testHostRpc.dispose();
            });
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
    public static async convertTestRequest(request: vscode.TestRunRequest, handler: (projects: vscode.TestItem[], filter: string[]) => Promise<void>): Promise<void> {
        if (request.include === undefined)
            return handler([], []);

        function getRootNode(item: vscode.TestItem): vscode.TestItem {
            if (item.parent === undefined)
                return item;
            return getRootNode(item.parent);
        }

        const projectItems: vscode.TestItem[] = [];
        const filter: string[] = [];
        request.include?.forEach(item => {
            const rootNode = getRootNode(item);
            if (!TestExplorerExtensions.isProjectItem(item))
                filter.push(item.id);
            if (!projectItems.includes(rootNode))
                projectItems.push(rootNode);
        });

        return handler(projectItems, filter);
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
    public static toTestMessage(error?: string, stackTrace?: string): vscode.TestMessage {
        if (error === undefined || stackTrace === undefined)
            return new vscode.TestMessage(error ?? "No error message provided");

        const lines = stackTrace.split('\n');
        for (const line of lines) {
            const match = line.match(/in (.+):line (\d+)/);
            if (match) {
                const filePath = match[1].trim();
                const lineNumber = parseInt(match[2], 10);
                const message = new vscode.TestMessage(`${error}\n\n${stackTrace}`);
                message.location = new vscode.Location(vscode.Uri.file(filePath), new vscode.Position(lineNumber - 1, 0));
                return message;
            }
        }

        return new vscode.TestMessage(`${error}\n\n${stackTrace}`);
    }
    public static toTestStatus(outcome: Outcome): string {
        if (outcome === Outcome.Passed)
            return `\x1b[32m${Outcome[outcome]}\x1b[0m`;
        if (outcome === Outcome.Failed)
            return `\x1b[31m${Outcome[outcome]}\x1b[0m`; // Red
        if (outcome === Outcome.NotFound)
            return `\x1b[33m${Outcome[outcome]}\x1b[0m`; // Yellow

        return Outcome[outcome];
    }
}
