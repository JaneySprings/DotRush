
export class PublicExports {
    public static instance: PublicExports;

    public onActiveProjectChanged: EventHandler;
    public onActiveConfigurationChanged: EventHandler;
    public onProjectsChanged: EventHandler;

    constructor() {
        PublicExports.instance = this;
        this.onActiveProjectChanged = new EventHandler();
        this.onActiveConfigurationChanged = new EventHandler();
        this.onProjectsChanged = new EventHandler();
    }

    public invokeAll() {
        this.onActiveProjectChanged.invoke(undefined);
        this.onActiveConfigurationChanged.invoke(undefined);
        this.onProjectsChanged.invoke(undefined);
    }
}

class EventHandler {
    private callbacks: Array<(data: any) => void>;
    private delayedData: any | undefined;

    constructor() {
        this.callbacks = [];
    }

    public add(callback: (data: any) => void) {
        this.callbacks.push(callback);
        if (this.delayedData !== undefined) {
            callback(this.delayedData);
            this.delayedData = undefined;
        }
    }
    public remove(callback: (data: any) => void) {
        const index = this.callbacks.indexOf(callback);
        if (index != -1 && index < this.callbacks.length)
            this.callbacks.splice(index, 1);
    }
    public invoke(data: any) {
        if (this.callbacks.length === 0) {
            this.delayedData = data;
            return;
        }
        this.callbacks.forEach(callback => callback(data));
    }
}