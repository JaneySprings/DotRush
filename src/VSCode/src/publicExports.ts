
export class PublicExports {
    public static instance: PublicExports;
    public projectChangedEventHandler: EventHandler;
    public configurationChangedEventHandler: EventHandler;

    constructor() {
        PublicExports.instance = this;
        this.projectChangedEventHandler = new EventHandler();
        this.configurationChangedEventHandler = new EventHandler();
    }

    public invokeAll() {
        this.projectChangedEventHandler.invoke(undefined);
        this.configurationChangedEventHandler.invoke(undefined);
    }
}

class EventHandler {
    private callbacks: Array<(data: any) => void>;

    constructor() {
        this.callbacks = [];
    }

    public add(callback: (data: any) => void) {
        this.callbacks.push(callback);
    }
    public remove(callback: (data: any) => void) {
        const index = this.callbacks.indexOf(callback);
        if (index != -1 && index < this.callbacks.length)
            this.callbacks.splice(index, 1);
    }
    public invoke(data: any) {
        this.callbacks.forEach(callback => callback(data));
    }
}