import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export function initializeComponents(webview: vscode.Webview, memoryViewerRoot: vscode.Uri, snapshotUri: vscode.Uri): string {
    const nonce = Math.random().toString(36).slice(2) + Math.random().toString(36).slice(2);
    const csp = [
        `default-src 'none'`,
        `style-src ${webview.cspSource} 'unsafe-inline'`,
        `script-src ${webview.cspSource} 'nonce-${nonce}'`,
        `connect-src ${webview.cspSource}`,
        `img-src ${webview.cspSource} data:`,
        `font-src ${webview.cspSource}`,
    ].join('; ');
    // The viewer exposes 'window.memoryViewer' once its bundle has run. The snapshot is fetched from the webview
    // resource origin, and the Compare view asks the extension to pick a baseline file through postMessage.
    const loader = `<script nonce="${nonce}">
(function () {
    const vscode = acquireVsCodeApi();
    const snapshotUri = ${JSON.stringify(webview.asWebviewUri(snapshotUri).toString())};
    const fileName = ${JSON.stringify(path.basename(snapshotUri.fsPath))};
    const pendingPicks = new Map();
    let nextRequestId = 1;

    window.addEventListener('message', event => {
        const message = event.data;
        if (message && message.type === 'snapshotPicked' && pendingPicks.has(message.requestId)) {
            pendingPicks.get(message.requestId)(message.url ? { url: message.url, fileName: message.fileName } : undefined);
            pendingPicks.delete(message.requestId);
        }
    });
    function waitForViewer() {
        return new Promise(resolve => {
            const check = () => window.memoryViewer !== undefined ? resolve(window.memoryViewer) : setTimeout(check, 50);
            check();
        });
    }
    function pickSnapshot() {
        return new Promise(resolve => {
            const requestId = nextRequestId++;
            pendingPicks.set(requestId, resolve);
            vscode.postMessage({ type: 'pickSnapshot', requestId });
        });
    }
    async function load() {
        const viewer = await waitForViewer();
        viewer.configure({ embedded: true, pickSnapshot });
        await viewer.loadFromUrl(snapshotUri, fileName);
    }
    load().catch(error => vscode.postMessage({ type: 'error', message: String(error && error.message || error) }));
})();
</script>`;

    // Rewrite the relative asset paths of the viewer's index.html to webview URIs and inject the CSP and the loader
    const html = fs.readFileSync(path.join(memoryViewerRoot.fsPath, 'index.html'), 'utf8');
    return html
        .replace(/(href|src)="([^":]+)"/g, (_, attribute, value) => `${attribute}="${webview.asWebviewUri(vscode.Uri.joinPath(memoryViewerRoot, value))}"`)
        .replace(/<head>/, `<head>\n    <meta http-equiv="Content-Security-Policy" content="${csp}">`)
        .replace(/<script /, `${loader}\n    <script `);
}
