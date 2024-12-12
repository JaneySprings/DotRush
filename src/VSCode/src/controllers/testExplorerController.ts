import { DotNetTaskProvider } from '../providers/dotnetTaskProvider';
import { TestExtensions } from '../models/test';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants'
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export class TestExplorerController {
    private static controller: vscode.TestController;
    private static testsResultDirectory: string;

    public static activate(context: vscode.ExtensionContext) {
        TestExplorerController.testsResultDirectory = path.join(context.extensionPath, "extension", "bin", "TestExplorer");

        TestExplorerController.controller = vscode.tests.createTestController(res.testExplorerViewId, res.testExplorerViewTitle);
        TestExplorerController.controller.refreshHandler = TestExplorerController.refreshTests;
        
        context.subscriptions.push(TestExplorerController.controller);
        context.subscriptions.push(TestExplorerController.controller.createRunProfile('Run NUnit/XUnit Tests', vscode.TestRunProfileKind.Run, TestExplorerController.runTests, true));
        context.subscriptions.push(TestExplorerController.controller.createRunProfile('Run Tests with Custom Configuration', vscode.TestRunProfileKind.Run, TestExplorerController.runTestsWithCustomConfig));
        context.subscriptions.push(TestExplorerController.controller.createRunProfile('Debug NUnit/XUnit Tests', vscode.TestRunProfileKind.Debug, TestExplorerController.debugTests, true));
        context.subscriptions.push(TestExplorerController.controller.createRunProfile('Debug Tests with Custom Configuration', vscode.TestRunProfileKind.Debug, TestExplorerController.debugTestsWithCustomConfig));
        TestExplorerController.refreshTests();
    }

    private static async refreshTests(): Promise<void> {
        TestExplorerController.controller.items.replace([]);

        const projects = await vscode.workspace.findFiles('**/*Tests.*csproj');
        for (const project of projects) {
            const projectName = path.basename(project.fsPath, '.csproj');
            const discoveredTests = await Interop.getTests(project.fsPath);
            if (discoveredTests.length === 0)
                continue;

            const root = TestExplorerController.controller.createTestItem(projectName, projectName, project);
            root.children.replace(discoveredTests.map(t => TestExtensions.toTestItem(t, TestExplorerController.controller)));
            TestExplorerController.controller.items.add(root);
        }
    }
    private static async runTests(request: vscode.TestRunRequest, token: vscode.CancellationToken): Promise<void> {
        TestExplorerController.convertTestRequest(request).forEach(async(filters, project) => {
            const testReport = TestExplorerController.getTestReportPath(project);
            const testArguments: string[] = [ '--logger',`'trx;LogFileName=${testReport}'` ];
            if (filters.length > 0) {
                testArguments.push('--filter');
                testArguments.push(`'${filters.join('|')}'`);
            }
            
            const testRun = TestExplorerController.controller.createTestRun(request);
            const execution = await vscode.tasks.executeTask(DotNetTaskProvider.getTestTask(project.uri!.fsPath, testArguments));
            await new Promise<boolean>((resolve) => vscode.tasks.onDidEndTaskProcess(e => {
                if (e.execution.task === execution.task)
                    resolve(e.exitCode === 0);
            }));

            await TestExplorerController.pushTestResults(testRun, project, testReport);
        });
    }
    private static async runTestsWithCustomConfig(request: vscode.TestRunRequest, token: vscode.CancellationToken): Promise<void> {
        TestExplorerController.convertTestRequest(request).forEach(async(filters, project) => {
            const testReport = TestExplorerController.getTestReportPath(project);
            const testRun = TestExplorerController.controller.createTestRun(request);
            // do

            await TestExplorerController.pushTestResults(testRun, project, testReport);
        });
    }
    private static async debugTests(request: vscode.TestRunRequest, token: vscode.CancellationToken): Promise<void> {
        TestExplorerController.convertTestRequest(request).forEach(async(filters, project) => {
            const execution = await vscode.tasks.executeTask(DotNetTaskProvider.getBuildTask(project.uri!.fsPath));
            const executionExitCode = await new Promise<number>((resolve) => vscode.tasks.onDidEndTaskProcess(e => {
                if (e.execution.task === execution.task)
                    resolve(e.exitCode ?? -1);
            }));

            if (executionExitCode !== 0 || token.isCancellationRequested)
                return;
            
            await vscode.debug.startDebugging(TestExplorerController.getWorkspaceFolder(), {
                name: request.profile?.label ?? 'Debug Tests',
                type: res.debuggerVsdbgId,
                request: 'attach',
                processId: await Interop.runTestHost(project.uri!.fsPath, filters.join('|'))
            });
        });
    }
    private static async debugTestsWithCustomConfig(request: vscode.TestRunRequest, token: vscode.CancellationToken): Promise<void> {
        TestExplorerController.convertTestRequest(request).forEach(async(filters, project) => {
            const customConfiguration = vscode.workspace.getConfiguration(res.extensionId).get<vscode.DebugConfiguration>('testExplorer.customDebugConfiguration');
            if (customConfiguration === undefined)
                return;
            
            customConfiguration.env = { VS_TEST_FILTER: filters.join(',') }
            await vscode.debug.startDebugging(TestExplorerController.getWorkspaceFolder(), customConfiguration);
        });
    }

    private static convertTestRequest(request: vscode.TestRunRequest) : Map<vscode.TestItem, string[]> {
        const testItems = new Map<vscode.TestItem, string[]>();
        const getRootNode = (item: vscode.TestItem) : vscode.TestItem => {
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
    private static pushTestResults(testRun: vscode.TestRun, project: vscode.TestItem, testReport: string): Promise<void> {
        const getAllChildren = (root: vscode.TestItem) => {
            const result: vscode.TestItem[] = [];
            const pushNodes = (node: vscode.TestItemCollection) => node.forEach(n => {
                result.push(n);
                pushNodes(n.children);
            });
            pushNodes(root.children);
            return result;
        }
        
        const testNodes = getAllChildren(project);
        return Interop.getTestResults(testReport).then(testResults => {
            testResults.forEach(result => {
                const duration = Number(result.duration.match(/\d+/g)?.join('')) / 10000;
                const testItem = testNodes.find(t => t.id === result.fullName);
                if (testItem === undefined)
                    return;

                if (result.state === 'Passed')
                    testRun.passed(testItem, duration);
                else if (result.state === 'Failed')
                    testRun.failed(testItem, TestExtensions.toTestMessage(result), duration);
                else
                    testRun.skipped(testItem);
            });
            testRun.end();
        });
    }
    public static getWorkspaceFolder() : vscode.WorkspaceFolder | undefined {
        if (vscode.workspace.workspaceFolders === undefined)
            return undefined;
        if (vscode.workspace.workspaceFolders.length === 1)
            return vscode.workspace.workspaceFolders[0];

        return undefined;
    }
    public static getTestReportPath(project: vscode.TestItem): string {
        const testReport = path.join(TestExplorerController.testsResultDirectory, `${project.label}.trx`);
        if (fs.existsSync(testReport))
            vscode.workspace.fs.delete(vscode.Uri.file(testReport));

        return testReport;
    }
}