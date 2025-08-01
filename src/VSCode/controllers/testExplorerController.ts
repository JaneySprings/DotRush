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

export class TestExplorerController {
    private static controller: vscode.TestController;

    public static activate(context: vscode.ExtensionContext) {
        TestExplorerController.controller = vscode.tests.createTestController(res.testExplorerViewId, res.testExplorerViewTitle);
        TestExplorerController.controller.refreshHandler = TestExplorerController.refreshTestItems;
        TestExplorerController.controller.resolveHandler = TestExplorerController.resolveTestItem;

        context.subscriptions.push(TestExplorerController.controller);
        context.subscriptions.push(TestExplorerController.controller.createRunProfile(res.testExplorerProfileRun, vscode.TestRunProfileKind.Run, async (r, ct) => await TestExplorerController.createTestRun(r, false, ct), true));
        context.subscriptions.push(TestExplorerController.controller.createRunProfile(res.testExplorerProfileDebug, vscode.TestRunProfileKind.Debug, async (r, ct) => await TestExplorerController.createTestRun(r, true, ct), true));

        context.subscriptions.push(vscode.workspace.onDidOpenTextDocument(async ev => await TestExplorerController.loadFixtures(ev.uri.fsPath)));
        context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async ev => await TestExplorerController.loadFixtures(ev.uri.fsPath)));
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
    private static async createTestRun(request: vscode.TestRunRequest, attachDebugger: boolean, token: vscode.CancellationToken): Promise<void> {
        return TestExplorerExtensions.convertTestRequest(request, async (projects, filter) => {
            if (projects.length === 0)
                TestExplorerController.controller.items.forEach(item => projects.push(item));

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

            const testAssemblies: string[] = [];
            for (const project of projects) {
                const targetPath = await Interop.getPropertyValue('TargetPath', project.uri!.fsPath, "Debug", undefined);
                testAssemblies.push(targetPath!);
            }
            
            if (testAssemblies.length === 0 || token.isCancellationRequested)
                return;

            const testRun = TestExplorerController.controller.createTestRun(request);
            const testHostRpc = Interop.createTestHostRpc(builder => {
                builder.append('-a', ...testAssemblies);
                builder.conditional('-d', () => attachDebugger);
                if (filter.length > 0)
                    builder.append('-f', ...filter);
            });
            testHostRpc.onNotification('handleMessage', (data: string) => testRun.appendOutput(`${data.trimEnd()}\r\n`));
            testHostRpc.onRequest('attachDebuggerToProcess', async (processId: number) => {
                await vscode.debug.startDebugging(Extensions.getWorkspaceFolder(), {
                    name: res.testExplorerProfileDebug,
                    type: res.debuggerNetCoreId,
                    processId: processId,
                    request: 'attach',
                });
                return true;
            });
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
    public static async convertTestRequest(request: vscode.TestRunRequest, handler: (projects: vscode.TestItem[], filter: string[]) => Promise<void>): Promise<void> {
        if (request.include === undefined)
            return handler([], []);

        const getRootNode = (item: vscode.TestItem): vscode.TestItem => {
            if (item.parent === undefined)
                return item;
            return getRootNode(item.parent);
        }
        
        const projectItems: vscode.TestItem[] = [];
        const filter = request.include.map(item => item.id);
        request.include?.forEach(item => {
            const rootNode = getRootNode(item);
            if (!projectItems.includes(rootNode))
                projectItems.push(rootNode);
        });

        return handler(projectItems, filter);
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
