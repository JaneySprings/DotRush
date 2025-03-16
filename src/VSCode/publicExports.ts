
export class PublicExports {
    public static instance: PublicExports;

    public onActiveProjectChanged: EventHandler;
    public onActiveConfigurationChanged: EventHandler;
    public onActiveFrameworkChanged: EventHandler;
    public onProjectLoaded: EventHandler;

    constructor() {
        PublicExports.instance = this;
        this.onActiveProjectChanged = new EventHandler();
        this.onActiveConfigurationChanged = new EventHandler();
        this.onActiveFrameworkChanged = new EventHandler();
        this.onProjectLoaded = new EventHandler();
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
        if (this.delayedData !== undefined)
            callback(this.delayedData);
    }
    public remove(callback: (data: any) => void) {
        const index = this.callbacks.indexOf(callback);
        if (index != -1 && index < this.callbacks.length)
            this.callbacks.splice(index, 1);
    }
    public invoke(data: any) {
        this.delayedData = data;
        this.callbacks.forEach(callback => callback(data));
    }
}