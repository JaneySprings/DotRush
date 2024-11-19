import { TestCaseExtensions } from '../models/test';
import { Interop } from '../interop/interop';
import * as res from '../resources/constants'
import * as vscode from 'vscode';
import * as path from 'path';

export class TestExplorerController {
    private static controller: vscode.TestController;
    // private static testsResultDirectory: string;

    public static activate(context: vscode.ExtensionContext) {
        // TestExplorerController.testsResultDirectory = path.join(context.extensionPath, "extension", "bin", "testResults");

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

            const root = TestExplorerController.controller.createTestItem(projectName, projectName);
            root.children.replace(discoveredTests.map(t => TestCaseExtensions.toTestItem(t, TestExplorerController.controller)));
            TestExplorerController.controller.items.add(root);
        }
    }


    private static async runTests(request: vscode.TestRunRequest, token: vscode.CancellationToken): Promise<void> {
        // TestController.convertTestRequest(request).forEach(async(value, key) => {
        //     const run = TestController.controller.createTestRun(request);
        //     const results = await TestController.runTestAdapter(value, key, run);
        //     if (results.length === 0) {
        //         run.end();
        //         return;
        //     }
            
        //     const testNodes = TestController.getFlattenTestNodes(key);
        //     results.forEach(result => {
        //         const duration = Number(result.duration.match(/\d+/g)?.join('')) / 10000;
        //         const testItem = testNodes.find(t => t.id === result.fullName);
        //         if (testItem === undefined)
        //             return;

        //         if (result.state === 'Passed')
        //             run.passed(testItem, duration);
        //         else if (result.state === 'Failed')
        //             run.failed(testItem, new vscode.TestMessage(result.errorMessage ?? ''), duration);
        //         else
        //             run.skipped(testItem);
        //     });
        //     run.end();
        // });
    }
    private static async debugTests(request: vscode.TestRunRequest, token: vscode.CancellationToken): Promise<void> {
        // TestController.convertTestRequest(request).forEach(async(value, key) => {
        //     const pid = await TestController.debugTestAdapter(value, key);
        //     if (pid <= 0)
        //         return;

        //     const run = TestController.controller.createTestRun(request);
        //     await vscode.debug.startDebugging(undefined, {
        //         "name": "Debug Tests",
        //         "type": res.debuggerVsdbgId,
        //         "request": "attach",
        //         "processId": pid
        //     });
        //     run.end();
        // });
    }

    // private static async runTestAdapter(tests: vscode.TestItem[], root: vscode.TestItem, run: vscode.TestRun) : Promise<TestResult[]> {
    //     const filter = tests.map(t => t.id).join('|');
    //     const testsResultFile = path.join(TestController.testsResultDirectory, `${root.label}.trx`);
    //     if (fs.existsSync(testsResultFile))
    //         vscode.workspace.fs.delete(vscode.Uri.file(testsResultFile));

    //     const result = await ProcessRunner.getResultAsync(new ProcessArgumentBuilder("dotnet")
    //         .append("test").appendQuoted(root.uri!.fsPath)
    //         .append("--logger").appendQuoted(`trx;LogFileName=${testsResultFile}`)
    //         .conditional('--filter', () => filter !== undefined && filter !== '')
    //         .conditional(`\"${filter}\"`, () => filter !== undefined && filter !== ''));

    //     run.appendOutput(result.stdout);
    //     run.appendOutput(result.stderr);
        
    //     if (!fs.existsSync(testsResultFile))
    //         return [];

    //     return await Interop.getTestsResult(testsResultFile);
    // }
    // private static async debugTestAdapter(tests: vscode.TestItem[], root: vscode.TestItem) : Promise<number> {
    //     const filter = tests.map(t => t.id).join('|');
    //     const builder = new ProcessArgumentBuilder("dotnet")
    //         .append("test").appendQuoted(root.uri!.fsPath)
    //         .conditional('--filter', () => filter !== undefined && filter !== '')
    //         .conditional(`\"${filter}\"`, () => filter !== undefined && filter !== '');

    //     await vscode.tasks.executeTask(new vscode.Task(
    //         {type: "process"}, vscode.TaskScope.Workspace, 
    //         "Start Test Process", "dotnet", 
    //         new vscode.ProcessExecution(builder.getCommand(), builder.getArguments(), { env: { "VSTEST_HOST_DEBUG": "1" } })
    //     ));

    //     return await new Promise<number>((resolve) => {
    //         const interval = setInterval(async () => {
    //             const processes = await Interop.getProcesses();
    //             const hostProcesses = processes.filter(p => p.name === 'testhost');
    //             if (hostProcesses.length > 0) {
    //                 clearInterval(interval);
    //                 resolve(hostProcesses.reduce((prev, current) => (prev.id > current.id) ? prev : current).id);
    //             }
    //         }, 500);
    //         setTimeout(() => {
    //             clearInterval(interval);
    //             resolve(-1);
    //         }, 120000);
    //     });
    // }
    // private static convertTestRequest(request: vscode.TestRunRequest) : Map<vscode.TestItem, vscode.TestItem[]> {
    //     const testItems = new Map<vscode.TestItem, vscode.TestItem[]>();
    //     const getRootNode = (item: vscode.TestItem) : vscode.TestItem => {
    //         if (item.parent === undefined)
    //             return item;
    //         return getRootNode(item.parent);
    //     }

    //     request.include?.forEach(item => {
    //         const rootNode = getRootNode(item);
    //         if (!testItems.has(rootNode))
    //             testItems.set(rootNode, []);
            
    //         if (rootNode === item)
    //             testItems.set(rootNode, []);
    //         else
    //             testItems.get(rootNode)?.push(item);
    //     });

    //     return testItems;
    // }
    // private static getFlattenTestNodes(root: vscode.TestItem) : vscode.TestItem[] {
    //     const result: vscode.TestItem[] = [];
    //     const pushNodes = (node: vscode.TestItemCollection) => node.forEach(n => {
    //         result.push(n);
    //         pushNodes(n.children);
    //     });
    //     pushNodes(root.children);
    //     return result;
    // }
}