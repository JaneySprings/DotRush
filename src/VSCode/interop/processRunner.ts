import { spawnSync, spawn } from 'child_process';
import { ProcessArgumentBuilder } from './processArgumentBuilder';
import { Extensions } from '../extensions';

export class ProcessRunner {
    public static runSync(builder: ProcessArgumentBuilder): string | undefined {
        const result = spawnSync(builder.getCommand(), builder.getArguments());
        if (result.error) {
            console.error(result.error);
            return undefined;
        }
        return result.stdout.toString().trimEnd();
    }
    public static runAsync<TModel>(builder: ProcessArgumentBuilder): Promise<TModel | undefined> {
        return new Promise((resolve) => {
            const child = spawn(builder.getCommand(), builder.getArguments(), {
                stdio: ['ignore', 'pipe', 'pipe'],
                detached: true,
            });

            let output = '';
            child.stdout?.on('data', (data) => {
                output += data.toString().trimEnd();
            });
            child.stderr?.on('data', (data) => {
                console.error(data.toString());
            });
            child.on('close', () => {
                resolve(Extensions.deserialize<TModel>(output));
            });
            child.unref();
        });
    }
    public static runDetached<TModel>(builder: ProcessArgumentBuilder): Promise<TModel | undefined> {
        return new Promise((resolve) => {
            const child = spawn(builder.getCommand(), builder.getArguments(), {
                detached: true,
                stdio: ['ignore', 'pipe', 'ignore'],
            });

            child.stdout?.on('data', (data) => {
                resolve(Extensions.deserialize<TModel>(data.toString().trimEnd()));
            });
            child.unref();
        });
    }
    public static createProcess(builder: ProcessArgumentBuilder): number | undefined {
        const child = spawn(builder.getCommand(), builder.getArguments(), {
            detached: true,
            stdio: ['ignore', 'ignore', 'ignore'],
            cwd: Extensions.getCurrentWorkingDirectory()
        });
        child.unref();
        return child.pid;
    }
}
