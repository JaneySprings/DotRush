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
        context.subscriptions.push(TestExplorerController.controller.createRunProfile('Run Tests', vscode.TestRunProfileKind.Run, TestExplorerController.runTests));
        context.subscriptions.push(TestExplorerController.controller.createRunProfile('Debug Tests', vscode.TestRunProfileKind.Debug, TestExplorerController.debugTests));
        TestExplorerController.refreshTests();
    }

    public static async refreshTests(): Promise<void> {
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
            if (project.uri === undefined)
                return;

            const testsReport = path.join(TestExplorerController.testsResultDirectory, `${project.label}.trx`);
            if (fs.existsSync(testsReport))
                vscode.workspace.fs.delete(vscode.Uri.file(testsReport));

            const testArguments: string[] = [ '--logger',`'trx;LogFileName=${testsReport}'` ];
            if (filters.length > 0) {
                testArguments.push('--filter');
                testArguments.push(`'${filters.join('|')}'`);
            }
            
            const testRun = TestExplorerController.controller.createTestRun(request);
            const execution = await vscode.tasks.executeTask(DotNetTaskProvider.getTestTask(project.uri?.fsPath, testArguments));
            await new Promise<void>((resolve, reject) => vscode.tasks.onDidEndTaskProcess(e => {
                if (e.execution.task === execution.task)
                    e.exitCode === 0 ? resolve() : reject();
            }));

            const testResults = await Interop.getTestResults(testsReport);
            const testNodes = TestExplorerController.getAllChildren(project);
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
    private static async debugTests(request: vscode.TestRunRequest, token: vscode.CancellationToken): Promise<void> {
        TestExplorerController.convertTestRequest(request).forEach(async(filters, project) => {
            if (project.uri === undefined)
                return;
            
            const testsReport = path.join(TestExplorerController.testsResultDirectory, `${project.label}.trx`);
            if (fs.existsSync(testsReport))
                vscode.workspace.fs.delete(vscode.Uri.file(testsReport));

            const execution = await vscode.tasks.executeTask(DotNetTaskProvider.getBuildTask(project.uri?.fsPath));
            await new Promise<void>((resolve, reject) => vscode.tasks.onDidEndTaskProcess(e => {
                if (e.execution.task === execution.task)
                    e.exitCode === 0 ? resolve() : reject();
            }));

            const testArguments: string[] = [ 
                project.uri.fsPath, '--no-build', `--logger trx;LogFileName=${testsReport}` 
            ];
            if (filters.length > 0) 
                testArguments.push(`--filter ${filters.join('|')}`);
    
            const pid = await Interop.runTestHost(testArguments.join(' '));
            await vscode.debug.startDebugging(undefined, {
                "name": "Debug Tests",
                "type": res.debuggerVsdbgId,
                "request": "attach",
                "processId": pid
            });
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
    private static getAllChildren(root: vscode.TestItem) : vscode.TestItem[] {
        const result: vscode.TestItem[] = [];
        const pushNodes = (node: vscode.TestItemCollection) => node.forEach(n => {
            result.push(n);
            pushNodes(n.children);
        });
        pushNodes(root.children);
        return result;
    }
}