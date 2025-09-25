import { ProcessArgumentBuilder } from './processArgumentBuilder';
import { ProcessRunner } from './processRunner';
import { Project } from '../models/project';
import { Process } from '../models/process';
import { Extensions } from '../extensions';
import { Status } from '../models/status';
import { spawn } from 'child_process';
import * as path from 'path';
import * as rpc from 'vscode-jsonrpc/node';


export class Interop {
    private static devHostPath: string;

    public static execExtension: string;
    public static binariesPath: string;

    public static initialize(extensionPath: string) {
        Interop.execExtension = process.platform === 'win32' ? '.exe' : '';
        Interop.binariesPath = path.join(extensionPath, "extension", "bin");
        Interop.devHostPath = path.join(Interop.binariesPath, "DevHost", "devhost.dll");
    }

    public static async getProject(projectFile: string): Promise<Project | undefined> {
        return await ProcessRunner.runAsync<Project>(new ProcessArgumentBuilder('dotnet')
            .append(Interop.devHostPath)
            .append("-p")
            .append(projectFile));
    }
    public static async getProcesses(): Promise<Process[] | undefined> {
        return await ProcessRunner.runAsync<Process[]>(new ProcessArgumentBuilder('dotnet')
            .append(Interop.devHostPath)
            .append("-ps"));
    }
    public static async installDebugger(id: string): Promise<Status | undefined> {
        return await ProcessRunner.runAsync<Status>(new ProcessArgumentBuilder('dotnet')
            .append(Interop.devHostPath)
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
    public static createTestHostRpc(configurator: (args: ProcessArgumentBuilder) => void): rpc.MessageConnection {
        const builder = new ProcessArgumentBuilder('dotnet').append(Interop.devHostPath);
        configurator(builder);

        const childProcess = spawn(builder.getCommand(), builder.getArguments(), { stdio: ['pipe', 'pipe', 'pipe'], cwd: Extensions.getCurrentWorkingDirectory() });
        const connection = rpc.createMessageConnection(
            new rpc.StreamMessageReader(childProcess.stdout),
            new rpc.StreamMessageWriter(childProcess.stdin)
        );

        connection.listen();
        return connection;
    }
}
