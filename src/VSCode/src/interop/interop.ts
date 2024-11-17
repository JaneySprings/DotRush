import { ProcessArgumentBuilder } from './processArgumentBuilder';
import { ProcessRunner } from './processRunner';
import { Project } from '../models/project';
import { Process } from '../models/process';
import { Status } from '../models/status';
import { TestCase } from '../models/test';
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

    public static async getProjects(folders: string[]): Promise<Project[]> {
        return await ProcessRunner.runAsync<Project[]>(new ProcessArgumentBuilder(Interop.workspacesToolPath)
            .append("--analyze-workspace")
            .append(...folders));
    }
    public static async getTests(projectFile: string): Promise<TestCase[]> {
        return await ProcessRunner.runAsync<TestCase[]>(new ProcessArgumentBuilder(Interop.testExplorerToolPath)
            .append("--list-tests")
            .append(projectFile));
    }
    public static async getProcesses(): Promise<Process[]> {
        return await ProcessRunner.runAsync<Process[]>(new ProcessArgumentBuilder(Interop.workspacesToolPath)
            .append("--list-proc"));
    }
    public static async installDebugger(): Promise<Status> {
        return await ProcessRunner.runAsync<Status>(new ProcessArgumentBuilder(Interop.workspacesToolPath)
            .append("--install-vsdbg"));
    }
    public static getPropertyValue(propertyName: string, project: Project, configuration: string) : string | undefined {
        return ProcessRunner.runSync(new ProcessArgumentBuilder("dotnet")
            .append("msbuild").append(project.path)
            .append(`-getProperty:${propertyName}`)
            .conditional(`-p:Configuration=${configuration}`, () => configuration));
    }
}
