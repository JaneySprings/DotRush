
export class ProcessArgumentBuilder {
    private command: string;
    private arguments: string[];

    public constructor(command: string) {
        this.command = command;
        this.arguments = [];
    }

    public getCommand(): string {
        return this.command;
    }
    public getArguments(): string[] {
        return this.arguments;
    }

    public append(...args: string[]): ProcessArgumentBuilder {
        args.forEach(a => this.arguments.push(a));
        return this;
    }
    public conditional(arg: string, condition: () => any): ProcessArgumentBuilder {
        if (condition())
            this.arguments.push(arg);
        return this;
    }
    public override(arg: string): ProcessArgumentBuilder {
        const argPair = arg.split("=");
        if (argPair.length < 2) {
            this.arguments.push(arg);
            return this;
        }
        const index = this.arguments.findIndex(a => a.startsWith(argPair[0]));
        if (index > -1) 
            this.arguments.splice(index, 1);
        if (argPair[1])
            this.arguments.push(arg);
        return this;
    }
    public build(): string {
        const command = `\"${this.getCommand()}\"`;
        const args = this.getArguments().map(arg => `\"${arg}\"`).join(' ');
        return `${command} ${args}`;
    }
}
