
export interface LaunchProfile {
    applicationUrl?: string;
    environmentVariables?: { [key: string]: string };
    commandLineArgs?: string[];
    executablePath?: string;
    workingDirectory?: string;
}