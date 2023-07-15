import { execSync } from 'child_process';
import * as res from './resources';
import * as vscode from 'vscode';


export class RuntimeController {
    public static targetFolderName: string;

    public static activate(context: vscode.ExtensionContext): boolean {
        const qualifiedVersion = RuntimeController.runtimeVersion();
        const qualifiedVersionRegex = new RegExp('^\\d+\\.\\d+', ''); 
        const versionRegexCollection = qualifiedVersionRegex.exec(qualifiedVersion);
        if (!versionRegexCollection || versionRegexCollection.length === 0) {
            vscode.window.showErrorMessage(res.messageRuntimeNotFound);
            return false;
        }

        RuntimeController.targetFolderName = `net${versionRegexCollection[0]}`;
        return true;
    }

    public static runtimeVersion(): string {
        return ProcessRunner.execSync("dotnet", "--version");
    }
}

class ProcessRunner {
    public static execSync(...args: string[]): string {
        return execSync(args.join(' ')).toString();
    }
}