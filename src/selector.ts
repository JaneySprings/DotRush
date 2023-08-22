import { execSync } from 'child_process';
import * as res from './resources';
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';


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

        const version = versionRegexCollection[0];
        const extensionPath = vscode.extensions.getExtension(`${res.extensionPublisher}.${res.extensionId}`)?.extensionPath ?? '';
        const extensionBinaryPath = path.join(extensionPath, "extension", "bin", `net${version}`);
        if (!fs.existsSync(extensionBinaryPath)) {
            vscode.window.showErrorMessage(res.messageEmbeddedRuntimeNotFound);
            return false;
        }

        RuntimeController.targetFolderName = `net${version}`;
        return true;
    }

    public static runtimeVersion(): string {
        return execSync("dotnet --version").toString();
    }
}