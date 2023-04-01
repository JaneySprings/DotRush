import { checkPluginInstalled, waitForActivation } from "../integration/extensionTools";
import { DotNetTaskProvider } from "../context/dotnetTaskProvider";
import { ClientController } from "./clientController";
import { Configuration } from "../configuration";
import { execSync } from "child_process";
import * as res from "../resources";
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';


export class SolutionController {
    private static frameworkList: string[];
    private static devicePlatform: string;

    public static async activate() {
        if (!checkPluginInstalled(res.extensionMeteorId))
            SolutionController.initializeWithDefaults();
        
        if (!Configuration.getSetting<boolean>(res.configurationIdUseMeteor)) {
            SolutionController.initializeWithDefaults();
            return;
        }
        
        const extensionContext = await waitForActivation(res.extensionMeteorId);
        if (extensionContext !== undefined)
            SolutionController.initializeWithMeteor(extensionContext);
    }


    private static async initializeWithDefaults() {
        const firstFolder = vscode.workspace.workspaceFolders?.[0];
        const projectPath = await SolutionController.findProject(firstFolder?.uri.fsPath ?? "");
        if (projectPath === undefined)
            return;
        ClientController.restart(SolutionController.prepareSolution(projectPath));
    }
    private static initializeWithMeteor(context: vscode.Extension<any>) {
        context?.exports.deviceChangedEventHandler.add((device: any) => {
            SolutionController.devicePlatform = device.platform;
            DotNetTaskProvider.targetFramework = SolutionController.frameworkList.find(f => {
                return f.includes(SolutionController.devicePlatform)
            });
        });
        context?.exports.projectChangedEventHandler.add((project: any) => {
            SolutionController.frameworkList = project.frameworks;
            DotNetTaskProvider.targetFramework = SolutionController.frameworkList.find(f => {
                return f.includes(SolutionController.devicePlatform)
            });
            ClientController.restart(SolutionController.prepareSolution(project.path));
        });
    }


    private static prepareSolution(csprojPath: string): string {
        const fileName = path.parse(csprojPath).name
        const targetDir = path.dirname(csprojPath);
        const target = path.join(targetDir, `${fileName}.sln`);
        
        if (!fs.existsSync(target)) {
            SolutionController.createSolution(targetDir, fileName);
            SolutionController.addProject(targetDir, csprojPath);
        }

        return target;
    }


    public static createSolution(directory: string, name: string) {
        const command = `dotnet new sln -n ${name}`;
        execSync(command, { cwd: directory });
    }
    public static addProject(directory: string, target: string) {
        const command = `dotnet sln add ${target}`;
        execSync(command, { cwd: directory });
    }
    public static async findProject(target: string): Promise<string | undefined> {
        if (target.endsWith(".csproj"))
            return target;
        const files = await vscode.workspace.fs.readDirectory(vscode.Uri.file(target));
        const project = files.find(f => f[0].endsWith(".csproj"));
        if (project === undefined)
            return undefined;
        return path.join(target, project[0]);
    }
}