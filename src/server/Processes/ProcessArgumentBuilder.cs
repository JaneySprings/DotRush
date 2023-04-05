namespace dotRush.Server.Processes;

public class ProcessArgumentBuilder {
    public List<string> args = new List<string>();

    public ProcessArgumentBuilder Append(string arg) {
        this.args.Add(arg);
        return this;
    }

    public ProcessArgumentBuilder Append(params string[] args) {
        this.args.AddRange(args);
        return this;
    }

    public ProcessArgumentBuilder AppendQuoted(string arg) {
        this.args.Add($"\"{arg}\"");
        return this;
    }

    public override string ToString() {
        return string.Join(' ', this.args);
    }
}