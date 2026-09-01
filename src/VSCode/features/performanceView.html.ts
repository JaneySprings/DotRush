export function initializeComponents(viewDurationSeconds: number): string {
    const nonce = Math.random().toString(36).slice(2) + Math.random().toString(36).slice(2);
    return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; script-src 'nonce-${nonce}';">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<style>
    html, body { margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; }
    #container { position: relative; width: 100%; height: 100%; }
    #chart { position: absolute; left: 0; top: 0; }
    #labels { position: absolute; top: 4px; left: 8px; font-size: 11px; }
    #labels .dot { display: inline-block; width: 8px; height: 8px; border-radius: 4px; margin-right: 5px; }
    #labels .value { opacity: 0.8; }
    #maxLabel { position: absolute; top: 4px; right: 8px; font-size: 11px; opacity: 0.7; }
    .timeLabel { position: absolute; bottom: 2px; font-size: 10px; opacity: 0.5; }
    #timeLeft { left: 8px; }
    #timeRight { right: 8px; }
    #noData { position: absolute; left: 0; top: 0; width: 100%; height: 100%; display: flex; align-items: center; justify-content: center; font-size: 12px; opacity: 0.6; }
</style>
</head>
<body>
<div id="container">
    <canvas id="chart"></canvas>
    <div id="labels"></div>
    <div id="maxLabel"></div>
    <div id="timeLeft" class="timeLabel">${viewDurationSeconds}s ago</div>
    <div id="timeRight" class="timeLabel">now</div>
    <div id="noData">No data available yet</div>
</div>
<script nonce="${nonce}">
(function () {
    const DURATION = ${viewDurationSeconds * 1000};

    const container = document.getElementById('container');
    const canvas = document.getElementById('chart');
    const labels = document.getElementById('labels');
    const maxLabel = document.getElementById('maxLabel');
    const noData = document.getElementById('noData');
    const ctx = canvas.getContext('2d');

    const sizeNames = ['B', 'KB', 'MB', 'GB', 'TB'];
    function formatSize(bytes) {
        let index = 0;
        while (bytes >= 1024 && index < sizeNames.length - 1) {
            bytes /= 1024;
            index++;
        }
        return (index === 0 || bytes >= 100 ? bytes.toFixed(0) : bytes.toFixed(1)) + ' ' + sizeNames[index];
    }

    // A series with a 'fixedMax' draws on its own scale instead of the shared byte ceiling
    const seriesDefs = [
        { key: 'cpuUsage', name: 'CPU', cssVar: '--vscode-charts-red', fixedMax: 100, format: value => value.toFixed(1) + '%' },
        { key: 'workingSet', name: 'Working Set', cssVar: '--vscode-charts-blue', format: formatSize },
    ];
    for (const series of seriesDefs) {
        const row = labels.appendChild(document.createElement('div'));
        row.innerHTML = '<span class="dot"></span>' + series.name + ': <span class="value"></span>';
        series.row = row;
        series.dot = row.firstElementChild;
        series.value = row.lastElementChild;
    }

    let samples = [];
    let frozen = false;
    let width = 0;
    let height = 0;
    let hoverX = undefined;

    window.addEventListener('message', event => {
        samples = event.data.samples;
        frozen = event.data.frozen && samples.length > 0;
    });
    canvas.addEventListener('mousemove', event => { hoverX = event.offsetX; });
    canvas.addEventListener('mouseleave', () => { hoverX = undefined; });

    function resize() {
        const ratio = window.devicePixelRatio || 1;
        width = container.clientWidth;
        height = container.clientHeight;
        canvas.width = width * ratio;
        canvas.height = height * ratio;
        canvas.style.width = width + 'px';
        canvas.style.height = height + 'px';
        ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    }
    new ResizeObserver(resize).observe(container);
    resize();

    function draw() {
        requestAnimationFrame(draw);
        if (width === 0 || height === 0)
            return;
        if (!frozen) {
            const cutoff = Date.now() - DURATION - 5000;
            while (samples.length > 0 && samples[0].timestamp < cutoff)
                samples.shift();
        }

        ctx.clearRect(0, 0, width, height);
        noData.style.display = samples.length === 0 ? 'flex' : 'none';
        if (samples.length === 0) {
            maxLabel.textContent = '';
            for (const series of seriesDefs)
                series.row.style.display = 'none';
            return;
        }

        // The byte axis ceiling snaps to a power of two so it doesn't jitter with every sample
        const maxY = Math.pow(2, Math.ceil(Math.log2(Math.max(1, ...samples.map(sample => sample.workingSet)))));
        maxLabel.textContent = formatSize(maxY);

        const style = getComputedStyle(document.documentElement);
        const now = frozen ? samples[samples.length - 1].timestamp : Date.now();
        const topPad = 22;
        const xFor = timestamp => width - (now - timestamp) / DURATION * width;
        const yFor = (value, seriesMax) => height - 1 - (value / seriesMax) * (height - topPad - 1);

        let hovered = undefined;
        if (hoverX !== undefined) {
            const hoverTime = now - (1 - hoverX / width) * DURATION;
            hovered = samples.reduce((best, sample) => Math.abs(sample.timestamp - hoverTime) < Math.abs(best.timestamp - hoverTime) ? sample : best);
            ctx.strokeStyle = style.getPropertyValue('--vscode-foreground').trim() || '#888';
            ctx.globalAlpha = 0.4;
            ctx.beginPath();
            const lineX = Math.round(Math.min(xFor(hovered.timestamp), width)) + 0.5;
            ctx.moveTo(lineX, 0);
            ctx.lineTo(lineX, height);
            ctx.stroke();
            ctx.globalAlpha = 1;
        }

        const latest = samples[samples.length - 1];
        for (const series of seriesDefs) {
            const seriesMax = series.fixedMax !== undefined ? series.fixedMax : maxY;
            const points = [];
            for (const sample of samples) {
                if (sample[series.key] !== undefined)
                    points.push([xFor(sample.timestamp), yFor(sample[series.key], seriesMax)]);
            }
            series.row.style.display = points.length === 0 ? 'none' : '';
            if (points.length === 0)
                continue;

            const color = style.getPropertyValue(series.cssVar).trim() || '#3794ff';
            const displayed = (hovered ?? latest)[series.key];
            series.dot.style.background = color;
            series.value.textContent = displayed !== undefined ? series.format(displayed) : '-';

            ctx.beginPath();
            ctx.moveTo(points[0][0], points[0][1]);
            for (let i = 1; i < points.length; i++)
                ctx.lineTo(points[i][0], points[i][1]);
            ctx.lineTo(width, points[points.length - 1][1]); // Hold the last value to 'now'
            ctx.strokeStyle = color;
            ctx.stroke();

            ctx.lineTo(width, height);
            ctx.lineTo(points[0][0], height);
            ctx.closePath();
            ctx.globalAlpha = 0.1;
            ctx.fillStyle = color;
            ctx.fill();
            ctx.globalAlpha = 1;
        }
    }
    requestAnimationFrame(draw);
})();
</script>
</body>
</html>`;
}
