import { ProcessArgumentBuilder } from '../interop/processArgumentBuilder';
import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { Interop } from '../interop/interop';
import { Project } from '../models/project';
import { Extensions } from '../extensions';
import { Icons } from '../resources/icons';
import * as res from '../resources/constants'
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import { LanguageServerController } from './languageServerController';
import { TestCase, TestFixture } from '../models/test';

export class TestExplorerController {
    private static controller: vscode.TestController;

    public static activate(context: vscode.ExtensionContext) {
        TestExplorerController.controller = vscode.tests.createTestController(res.testExplorerViewId, res.testExplorerViewTitle);
        TestExplorerController.controller.refreshHandler = TestExplorerController.reloadProjects;
        TestExplorerController.controller.resolveHandler = TestExplorerController.resolveTestItem;
        
        context.subscriptions.push(TestExplorerController.controller);
        // context.subscriptions.push(TestExplorerController.controller.createRunProfile(res.testExplorerProfileRun, vscode.TestRunProfileKind.Run, TestExplorerController.runTests, true));
        // context.subscriptions.push(TestExplorerController.controller.createRunProfile(res.testExplorerProfileDebug, vscode.TestRunProfileKind.Debug, TestExplorerController.debugTests, true));
        /* Experimental API */
        // if (Extensions.getSetting(res.configIdTestExplorerAutoRefreshTests)) {
        //     context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(ev => {
        //         const fileName = path.basename(ev.uri.fsPath);
        //         if (fileName.endsWith('.cs') && fileName.includes('Test'))
        //             TestExplorerController.refreshTests();
        //     }));
        // }
        // if (Extensions.getSetting(res.configIdTestExplorerSkipInitialPauseEvent) && Extensions.onVSCode(true, false)) {
        //     context.subscriptions.push(vscode.debug.registerDebugAdapterTrackerFactory(res.debuggerNetCoreId, new ContinueAfterInitialPauseTracker()));
        // }
    }

    public static loadProject(project: Project): void {
        const item = TestExplorerExtensions.createProjectItem(TestExplorerController.controller, project);
        TestExplorerController.controller.items.add(item);
    }
    public static unloadProjects() {
        TestExplorerController.controller.items.replace([]);
    }
    public static reloadProjects() {

    }

    private static async resolveTestItem(item: vscode.TestItem | undefined): Promise<void> {
        if (item === undefined || item.children.size > 0)
            return;

        if (TestExplorerExtensions.isProjectItem(item)) {
            const fixtures = await LanguageServerController.sendRequest<TestFixture[]>('dotrush/testExplorer/fixtures', { textDocument: Extensions.documentIdFromUri(item.uri)});
            if (fixtures !== undefined && fixtures.length > 0)
                item.children.replace(fixtures.map(fixture => TestExplorerExtensions.createFixtureItem(TestExplorerController.controller, fixture)));
        }
        else if (TestExplorerExtensions.isFixtureItem(item)) {
            const testCases = await LanguageServerController.sendRequest<TestCase[]>('dotrush/testExplorer/tests', { textDocument: Extensions.documentIdFromUri(item.uri)});
            if (testCases !== undefined && testCases.length > 0)
                item.children.replace(testCases.map(testCase => TestExplorerExtensions.createTestCaseItem(TestExplorerController.controller, testCase)));
        }
    }
    
}

class TestExplorerExtensions {
    public static createProjectItem(controller: vscode.TestController, project: Project): vscode.TestItem {
        const item = controller.createTestItem(project.name, `${Icons.solution} ${project.name}`, vscode.Uri.file(project.path));
        item.canResolveChildren = true;
        return item;
    }
    public static createFixtureItem(controller: vscode.TestController, fixture: TestFixture): vscode.TestItem {
        const item = controller.createTestItem(fixture.id, `${Icons.module} ${fixture.name}`, vscode.Uri.file(fixture.filePath));
        item.range = fixture.range;
        item.canResolveChildren = true;
        return item;
    }
    public static createTestCaseItem(controller: vscode.TestController, testCase: TestCase): vscode.TestItem {
        const item = controller.createTestItem(testCase.id, `${Icons.test} ${testCase.name}`, vscode.Uri.file(testCase.filePath));
        item.range = testCase.range;
        item.canResolveChildren = false;
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

    // public static toTestMessage(testResult: TestResult): TestMessage {
    //     let message = testResult.errorMessage ?? '';
    //     if (testResult.stackTrace !== null)
    //         message += `\n\n${testResult.stackTrace}`;
    //     return new TestMessage(message);
    // }
    // public static toDurationNumber(duration: string | null): number {
    //     const match = duration?.match(/(\d+):(\d+):(\d+)\.(\d+)/);
    //     if (duration === null || !match)
    //         return 1;

    //     const [_, hours, minutes, seconds, milliseconds] = match;
    //     return parseInt(hours, 10) * 60 * 60 * 1000 +
    //         parseInt(minutes, 10) * 60 * 1000 +
    //         parseInt(seconds, 10) * 1000 +
    //         parseInt(milliseconds.slice(0, 3), 10);
    // }
}

//     private static async refreshTests(): Promise<void> {
//         await vscode.workspace.saveAll();

//         const projectFiles: string[] = [];
//         TestExplorerController.controller.items.forEach(item => projectFiles.push(item.uri!.fsPath));
//         return Extensions.parallelForEach(projectFiles, async (projectFile) => await TestExplorerController.loadProject(projectFile));
//     }
//     private static async runTests(request: vscode.TestRunRequest, token: vscode.CancellationToken): Promise<void> {
//         TestExplorerController.convertTestRequest(request).forEach(async (filters, project) => {
//             const preLaunchTask = await Extensions.getTask(Extensions.getSetting<string>(res.configIdTestExplorerPreLaunchTask));
//             const testReport = path.join(TestExplorerController.testsResultDirectory, `${project.id}.trx`);
//             if (fs.existsSync(testReport))
//                 vscode.workspace.fs.delete(vscode.Uri.file(testReport));

//             const testArguments = new ProcessArgumentBuilder('test')
//                 .append('--logger').append(`'trx;LogFileName=${testReport}'`);

//             testArguments.conditional('--no-build', () => preLaunchTask !== undefined);
//             testArguments.conditional('--filter', () => filters.length > 0);
//             testArguments.conditional(`'${filters.join('|')}'`, () => filters.length > 0);

//             const testRun = TestExplorerController.controller.createTestRun(request);
//             if (preLaunchTask !== undefined) {
//                 const executionSuccess = await Extensions.waitForTask(preLaunchTask);
//                 if (!executionSuccess || token.isCancellationRequested)
//                     return;
//             }
//             await Extensions.waitForTask(DotNetTaskProvider.getTestTask(project.uri!.fsPath, testArguments));
//             await TestExplorerController.publishTestResults(testRun, project, testReport);
//         });
//     }
//     private static async debugTests(request: vscode.TestRunRequest, token: vscode.CancellationToken): Promise<void> {
//         TestExplorerController.convertTestRequest(request).forEach(async (filters, project) => {
//             const preLaunchTask = await Extensions.getTask(Extensions.getSetting<string>(res.configIdTestExplorerPreLaunchTask));
//             const executionSuccess = await Extensions.waitForTask(preLaunchTask ?? DotNetTaskProvider.getBuildTask(project.uri!.fsPath));
//             if (!executionSuccess || token.isCancellationRequested)
//                 return;

//             const processId = await Interop.runTestHost(project.uri!.fsPath, filters.join('|'));
//             await vscode.debug.startDebugging(Extensions.getWorkspaceFolder(), {
//                 name: res.testExplorerProfileDebug,
//                 type: res.debuggerNetCoreId,
//                 processId: processId,
//                 request: 'attach',
//             });
//         });
//     }

//     private static convertTestRequest(request: vscode.TestRunRequest): Map<vscode.TestItem, string[]> {
//         const testItems = new Map<vscode.TestItem, string[]>();
//         const getRootNode = (item: vscode.TestItem): vscode.TestItem => {
//             if (item.parent === undefined)
//                 return item;
//             return getRootNode(item.parent);
//         }

//         if (request.include === undefined) {
//             TestExplorerController.controller.items.forEach(item => testItems.set(item, []));
//             return testItems;
//         }

//         request.include?.forEach(item => {
//             const rootNode = getRootNode(item);
//             if (!testItems.has(rootNode))
//                 testItems.set(rootNode, []);
//             if (item.id !== rootNode.id)
//                 testItems.get(rootNode)?.push(item.id);
//         });

//         return testItems;
//     }
//     private static publishTestResults(testRun: vscode.TestRun, project: vscode.TestItem, testReport: string): Promise<void> {
//         const findTestItem = (id: string) => {
//             const fixtureId = id.substring(0, id.lastIndexOf('.'));
//             const fixture = project.children.get(fixtureId);
//             if (fixture === undefined)
//                 return undefined;

//             return fixture.children.get(id);
//         }

//         return Interop.getTestResults(testReport).then(testResults => {
//             testResults?.forEach(result => {
//                 const duration = TestExtensions.toDurationNumber(result.duration);
//                 const testItem = findTestItem(result.fullName);
//                 if (testItem === undefined)
//                     return;

//                 if (result.state === 'Passed')
//                     testRun.passed(testItem, duration);
//                 else if (result.state === 'Failed')
//                     testRun.failed(testItem, TestExtensions.toTestMessage(result), duration);
//                 else
//                     testRun.skipped(testItem);
//             });
//             testRun.end();
//         });
//     }
// }

// /* Experimental API */
// // https://github.com/microsoft/vstest/blob/06101ef5feb95048cbe850472ed49604863d54ff/src/vstest.console/Program.cs#L37
// class ContinueAfterInitialPauseTracker implements vscode.DebugAdapterTrackerFactory {
//     private isInitialStoppedEvent: boolean = false;

//     public createDebugAdapterTracker(session: vscode.DebugSession): vscode.ProviderResult<vscode.DebugAdapterTracker> {
//         const self = this;
//         return {
//             onDidSendMessage(message: any) {
//                 if (session.name !== res.testExplorerProfileDebug)
//                     return;

//                 if (message.type == 'response' && message.command == 'initialize')
//                     self.isInitialStoppedEvent = true;
//                 if (message.type == 'event' && message.event == 'stopped' && self.isInitialStoppedEvent) {
//                     session.customRequest('continue', { threadId: message.body.threadId });
//                     self.isInitialStoppedEvent = false;
//                 }
//             }
//         }
//     }
// }