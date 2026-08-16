import { ProcessArgumentBuilder } from './processArgumentBuilder';
import { ProcessRunner } from './processRunner';
import { TemplateInfo } from '../models/template';
import { Project } from '../models/project';
import { Process } from '../models/process';
import { Extensions } from '../extensions';
import { Status } from '../models/status';
import { spawn } from 'child_process';
import * as res from '../resources/constants';
import * as path from 'path';
import * as fs from 'fs';
import * as vscode from 'vscode';
import * as rpc from 'vscode-jsonrpc/node';

export class Interop {
    private static devHostPath: string;
    public static execExtension: string;
    public static binariesPath: string;
    public static dotnetPath: string;

    public static async initialize(extensionPath: string): Promise<Status> {
        Interop.execExtension = process.platform === 'win32' ? '.exe' : '';
        Interop.binariesPath = path.join(extensionPath, "extension", "bin");
        Interop.devHostPath = path.join(Interop.binariesPath, "DevHost", "devhost.dll");

        const dotnetSdkDirectory = Extensions.getSetting<string>(res.configIdRoslynDotnetSdkDirectory);
        Interop.dotnetPath = dotnetSdkDirectory
            ? path.join(dotnetSdkDirectory, "..", "..", "dotnet" + Interop.execExtension)
            : path.join(Interop.binariesPath, "Sdk", "dotnet" + Interop.execExtension);
        if (!fs.existsSync(Interop.dotnetPath))
            Interop.dotnetPath = "dotnet";

        if (Interop.getDevHostVersion() === undefined)
            return new StatusImpl(res.messageInvalidDotnetSdk);

        const dotnetDebuggerPath = path.join(Interop.binariesPath, "Debugger", "clrdbg" + Interop.execExtension);
        if (!fs.existsSync(dotnetDebuggerPath)) {
            Interop.installDebugger(Extensions.onVSCode(res.debuggerVsdbgInstallId, res.debuggerSharpdbgInstallId)).then(result => {
                if (result !== undefined && !result.isSuccess) // Not a blocker, run intellisense only
                    vscode.window.showErrorMessage(`${res.messageInstallingComponentFailed}: ${result.message}`);
            });
        }

        return new StatusImpl();
    }

    public static getProject(projectFile: string): Promise<Project | undefined> {
        return ProcessRunner.runAsync<Project>(new ProcessArgumentBuilder(Interop.dotnetPath)
            .append(Interop.devHostPath)
            .append("-p")
            .append(projectFile));
    }
    public static getProcesses(): Promise<Process[] | undefined> {
        return ProcessRunner.runAsync<Process[]>(new ProcessArgumentBuilder(Interop.dotnetPath)
            .append(Interop.devHostPath)
            .append("-ps"));
    }
    public static getTemplates(): Promise<TemplateInfo[] | undefined> {
        return ProcessRunner.runAsync<TemplateInfo[]>(new ProcessArgumentBuilder(Interop.dotnetPath)
            .append(Interop.devHostPath)
            .append("new")
            .append("-l"));
    }
    public static getDevHostVersion(): string | undefined {
        return ProcessRunner.runSync(new ProcessArgumentBuilder(Interop.dotnetPath)
            .append(Interop.devHostPath)
            .append("--version"));
    }
    public static getPropertyValue(propertyName: string, projectPath: string, configuration: string | undefined, framework: string | undefined): string | undefined {
        return ProcessRunner.runSync(new ProcessArgumentBuilder(Interop.dotnetPath)
            .append("msbuild").append(projectPath)
            .append(`-getProperty:${propertyName}`)
            .conditional(`-p:Configuration=${configuration}`, () => configuration)
            .conditional(`-p:TargetFramework=${framework}`, () => framework));
    }
    public static createTemplate(identity: string, output: string, parameters: { [key: string]: string }): Promise<Status | undefined> {
        return ProcessRunner.runAsync<Status>(new ProcessArgumentBuilder(Interop.dotnetPath)
            .append(Interop.devHostPath).append("new")
            .append("-i", identity)
            .append("-o", output)
            .append("-p", JSON.stringify(parameters)));
    }

    public static createProcess(executable: string, cwd: string | undefined): number | undefined {
        return ProcessRunner.createProcess(new ProcessArgumentBuilder(executable), cwd);
    }
    public static createTestHostRpc(configurator: (args: ProcessArgumentBuilder) => void): rpc.MessageConnection {
        const builder = new ProcessArgumentBuilder(Interop.dotnetPath).append(Interop.devHostPath).append('test');
        configurator(builder);

        const childProcess = spawn(builder.getCommand(), builder.getArguments(), { stdio: ['pipe', 'pipe', 'pipe'], cwd: Extensions.getCurrentWorkingDirectory() });
        const connection = rpc.createMessageConnection(
            new rpc.StreamMessageReader(childProcess.stdout),
            new rpc.StreamMessageWriter(childProcess.stdin)
        );

        connection.listen();
        return connection;
    }

    private static installDebugger(id: string): Thenable<Status | undefined> {
        const getNameByDebuggerId = (id: string) => {
            switch (id) {
                case res.debuggerVsdbgInstallId: return res.debuggerVsdbgDisplayName;
                case res.debuggerSharpdbgInstallId: return res.debuggerSharpdbgDisplayName;
                default: return id;
            }
        };
        const options: vscode.ProgressOptions = {
            title: res.messageInstallingComponentTitle + getNameByDebuggerId(id),
            location: vscode.ProgressLocation.Notification,
            cancellable: false
        };
        return vscode.window.withProgress(options, (_p, _ct) => {
            return ProcessRunner.runAsync<Status>(new ProcessArgumentBuilder(Interop.dotnetPath)
                .append(Interop.devHostPath)
                .append(`-${id}`));
        });
    }
}

class StatusImpl implements Status {
    isSuccess: boolean;
    message: string | null;

    constructor(message: string | null = null) {
        this.isSuccess = message === null;
        this.message = message;
    }
}