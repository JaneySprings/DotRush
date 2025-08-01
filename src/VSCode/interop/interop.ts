import { ProcessArgumentBuilder } from './processArgumentBuilder';
import { ProcessRunner } from './processRunner';
import { Project } from '../models/project';
import { Process } from '../models/process';
import { Status } from '../models/status';
import { spawn } from 'child_process';
import * as path from 'path';
import * as rpc from 'vscode-jsonrpc/node';
import * as vscode from 'vscode';


export class Interop {
    private static testHostPath: string;

    public static execExtension: string;
    public static binariesPath: string;

    public static initialize(extensionPath: string) {
        Interop.execExtension = process.platform === 'win32' ? '.exe' : '';
        Interop.binariesPath = path.join(extensionPath, "extension", "bin");
        Interop.testHostPath = path.join(Interop.binariesPath, "TestHost", "testhost.dll");
    }

    public static async getProject(projectFile: string): Promise<Project | undefined> {
        return await ProcessRunner.runAsync<Project>(new ProcessArgumentBuilder('dotnet')
            .append(Interop.testHostPath)
            .append("-p")
            .append(projectFile));
    }
    public static async getProcesses(): Promise<Process[] | undefined> {
        return await ProcessRunner.runAsync<Process[]>(new ProcessArgumentBuilder('dotnet')
            .append(Interop.testHostPath)
            .append("-ps"));
    }
    public static async installDebugger(id: string): Promise<Status | undefined> {
        return await ProcessRunner.runAsync<Status>(new ProcessArgumentBuilder('dotnet')
            .append(Interop.testHostPath)
            .append(`-${id}`));
    }
    public static getPropertyValue(propertyName: string, projectPath: string, configuration: string | undefined, framework: string | undefined): string | undefined {
        return ProcessRunner.runSync(new ProcessArgumentBuilder("dotnet")
            .append("msbuild").append(projectPath)
            .append(`-getProperty:${propertyName}`)
            .conditional(`-p:Configuration=${configuration}`, () => configuration)
            .conditional(`-p:TargetFramework=${framework}`, () => framework));
    }

    public static createProcess(executable: string): number | undefined {
        return ProcessRunner.createProcess(new ProcessArgumentBuilder(executable));
    }
    public static createTestHostRpc(args: string[]): rpc.MessageConnection {
        const finalArgs = [ Interop.testHostPath, ...args ];
        const childProcess = spawn('dotnet', finalArgs, { stdio: ['pipe', 'pipe', 'pipe'] });
        const connection = rpc.createMessageConnection(
            new rpc.StreamMessageReader(childProcess.stdout),
            new rpc.StreamMessageWriter(childProcess.stdin)
        );

        connection.listen();
        return connection;
    }
}
