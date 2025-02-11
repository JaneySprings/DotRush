import { ProcessArgumentBuilder } from './processArgumentBuilder';
import { ProcessRunner } from './processRunner';
import { Project } from '../models/project';
import { Process } from '../models/process';
import { Status } from '../models/status';
import { TestFixture, TestResult } from '../models/test';
import * as path from 'path';


export class Interop {
    private static workspacesToolPath: string;
    private static testExplorerToolPath: string;

    public static execExtension: string;

    public static initialize(extensionPath : string) {
        Interop.execExtension = process.platform === 'win32' ? '.exe' : '';
        Interop.workspacesToolPath = path.join(extensionPath, "extension", "bin", "Workspaces", "DotRush.Essentials.Workspaces" + Interop.execExtension);
        Interop.testExplorerToolPath = path.join(extensionPath, "extension", "bin", "TestExplorer", "DotRush.Essentials.TestExplorer" + Interop.execExtension);
    }

    public static async getProject(projectFile: string): Promise<Project> {
        return await ProcessRunner.runAsync<Project>(new ProcessArgumentBuilder(Interop.workspacesToolPath)
            .append("--project")
            .append(projectFile));
    }
    public static async getTests(projectFile: string): Promise<TestFixture[]> {
        return await ProcessRunner.runAsync<TestFixture[]>(new ProcessArgumentBuilder(Interop.testExplorerToolPath)
            .append("--list-tests")
            .append(projectFile));
    }
    public static async getTestResults(reportFile: string): Promise<TestResult[]> {
        return await ProcessRunner.runAsync<TestResult[]>(new ProcessArgumentBuilder(Interop.testExplorerToolPath)
            .append("--convert")
            .append(reportFile));
    }
    public static async getProcesses(): Promise<Process[]> {
        return await ProcessRunner.runAsync<Process[]>(new ProcessArgumentBuilder(Interop.workspacesToolPath)
            .append("--list-proc"));
    }
    public static async installDebugger(): Promise<Status> {
        return await ProcessRunner.runAsync<Status>(new ProcessArgumentBuilder(Interop.workspacesToolPath)
            .append("--install-vsdbg"));
    }
    public static async runTestHost(projectFile: string, filter: string): Promise<number> {
        return await ProcessRunner.runDetached<number>(new ProcessArgumentBuilder(Interop.testExplorerToolPath)
            .append("--run")
            .append(projectFile)
            .append(filter));
    }
    public static getPropertyValue(propertyName: string, projectPath: string, configuration: string | undefined, framework: string | undefined) : string | undefined {
        return ProcessRunner.runSync(new ProcessArgumentBuilder("dotnet")
            .append("msbuild").append(projectPath)
            .append(`-getProperty:${propertyName}`)
            .conditional(`-p:Configuration=${configuration}`, () => configuration)
            .conditional(`-p:TargetFramework=${framework}`, () => framework));
    }
}
