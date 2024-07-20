
export class Workspace {
    path: string;
    projects: string[];

    constructor(path: string, projects: string[] = []) {
        this.path = path;
        this.projects = projects;
    }
}