
export class ProcessArgumentBuilder {
    private arguments: string[] = [];

    public constructor(command: string) {
        this.arguments.push(command);
    }

    public append(...arg: string[]): ProcessArgumentBuilder {
        arg.forEach(a => this.arguments.push(a));
        return this;
    }
    public appendQuoted(...args: string[]): ProcessArgumentBuilder {
        args.forEach(a => this.arguments.push(`"${a}"`));
        return this;
    }
    public override(arg: string): ProcessArgumentBuilder {
        const argName = arg.split("=")[0];
        const index = this.arguments.findIndex(a => a.startsWith(argName));
        if (index > -1) 
            this.arguments.splice(index, 1);
        this.arguments.push(arg);
        return this;
    }
    public build(): string {
        return this.arguments.join(" ");
    }
}
