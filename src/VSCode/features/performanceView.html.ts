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
    .header { position: absolute; left: 8px; right: 8px; padding-top: 4px; display: flex; justify-content: space-between; gap: 8px; font-size: 11px; pointer-events: none; }
    .labels { display: flex; flex-wrap: wrap; gap: 0 12px; }
    .labels .dot { display: inline-block; box-sizing: border-box; width: 8px; height: 8px; border-radius: 4px; border: 2px solid; margin-right: 5px; }
    .labels .value { opacity: 0.8; }
    .maxLabel { opacity: 0.7; white-space: nowrap; }
    .timeLabel { position: absolute; bottom: 2px; font-size: 10px; opacity: 0.5; }
    #timeLeft { left: 8px; }
    #timeRight { right: 8px; }
    #noData { position: absolute; left: 0; top: 0; width: 100%; height: 100%; display: flex; align-items: center; justify-content: center; font-size: 12px; opacity: 0.6; }
</style>
</head>
<body>
<div id="container">
    <canvas id="chart"></canvas>
    <div id="timeLeft" class="timeLabel">${viewDurationSeconds}s ago</div>
    <div id="timeRight" class="timeLabel">now</div>
    <div id="noData">No data available yet</div>
</div>
<script nonce="${nonce}">
(function () {
    const DURATION = ${viewDurationSeconds * 1000};

    const container = document.getElementById('container');
    const canvas = document.getElementById('chart');
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

    // Every panel has a single scale shared by its series. The second series of a panel is dashed and has no fill,
    // so the two stay distinguishable without relying on color alone
    const panels = [
        { fixedMax: 100, format: value => value.toFixed(1) + '%', series: [
            { key: 'cpuUsage', name: 'CPU', cssVar: '--vscode-charts-red' },
            { key: 'timeInGC', name: 'Time in GC', cssVar: '--vscode-charts-yellow', dashed: true },
        ] },
        { format: formatSize, series: [
            { key: 'workingSet', name: 'Working Set', cssVar: '--vscode-charts-blue' },
            { key: 'gcHeapSize', name: 'GC Heap', cssVar: '--vscode-charts-green', dashed: true },
        ] },
    ];
    for (const panel of panels) {
        panel.header = container.appendChild(document.createElement('div'));
        panel.header.className = 'header';
        panel.header.innerHTML = '<div class="labels"></div><div class="maxLabel"></div>';
        panel.maxLabel = panel.header.lastElementChild;
        for (const series of panel.series) {
            const row = panel.header.firstElementChild.appendChild(document.createElement('div'));
            row.innerHTML = '<span class="dot"></span>' + series.name + ': <span class="value"></span>';
            series.row = row;
            series.dot = row.firstElementChild;
            series.value = row.lastElementChild;
        }
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

        // A panel without data gives its space to the others, e.g. when the runtime counters are not available
        const visiblePanels = panels.filter(panel => panel.series.some(series => samples.some(sample => sample[series.key] !== undefined)));
        for (const panel of panels)
            panel.header.style.display = visiblePanels.includes(panel) ? '' : 'none';
        if (visiblePanels.length === 0)
            return;

        const style = getComputedStyle(document.documentElement);
        const foreground = style.getPropertyValue('--vscode-foreground').trim() || '#888';
        const now = frozen ? samples[samples.length - 1].timestamp : Date.now();
        const topPad = 22;
        const panelHeight = height / visiblePanels.length;
        const xFor = timestamp => width - (now - timestamp) / DURATION * width;

        let hovered = undefined;
        if (hoverX !== undefined) {
            const hoverTime = now - (1 - hoverX / width) * DURATION;
            hovered = samples.reduce((best, sample) => Math.abs(sample.timestamp - hoverTime) < Math.abs(best.timestamp - hoverTime) ? sample : best);
            ctx.strokeStyle = foreground;
            ctx.globalAlpha = 0.4;
            ctx.beginPath();
            const lineX = Math.round(Math.min(xFor(hovered.timestamp), width)) + 0.5;
            ctx.moveTo(lineX, 0);
            ctx.lineTo(lineX, height);
            ctx.stroke();
            ctx.globalAlpha = 1;
        }

        const latest = samples[samples.length - 1];
        visiblePanels.forEach((panel, index) => {
            const bottom = (index + 1) * panelHeight;
            panel.header.style.top = index * panelHeight + 'px';
            if (index > 0) {
                ctx.strokeStyle = foreground;
                ctx.globalAlpha = 0.15;
                ctx.beginPath();
                ctx.moveTo(0, Math.round(bottom - panelHeight) + 0.5);
                ctx.lineTo(width, Math.round(bottom - panelHeight) + 0.5);
                ctx.stroke();
                ctx.globalAlpha = 1;
            }

            // An open ceiling snaps to a power of two so it doesn't jitter with every sample
            const panelMax = panel.fixedMax ?? Math.pow(2, Math.ceil(Math.log2(Math.max(1, ...panel.series.flatMap(series => samples.map(sample => sample[series.key] ?? 0))))));
            const yFor = value => bottom - 1 - (value / panelMax) * (panelHeight - topPad - 1);
            panel.maxLabel.textContent = panel.format(panelMax);

            for (const series of panel.series) {
                // Samples without a value split the line, a gap must not look like measured data
                const segments = [];
                let segment = undefined;
                for (const sample of samples) {
                    if (sample[series.key] === undefined) {
                        segment = undefined;
                        continue;
                    }
                    if (segment === undefined)
                        segments.push(segment = []);
                    segment.push([xFor(sample.timestamp), yFor(sample[series.key])]);
                }
                series.row.style.display = segments.length === 0 ? 'none' : '';
                if (segments.length === 0)
                    continue;

                const color = style.getPropertyValue(series.cssVar).trim() || '#3794ff';
                const displayed = (hovered ?? latest)[series.key];
                series.dot.style.borderColor = color;
                series.dot.style.background = series.dashed ? 'transparent' : color;
                series.value.textContent = displayed !== undefined ? panel.format(displayed) : '-';

                for (const points of segments) {
                    ctx.beginPath();
                    ctx.moveTo(points[0][0], points[0][1]);
                    for (let i = 1; i < points.length; i++)
                        ctx.lineTo(points[i][0], points[i][1]);
                    if (points === segment)
                        ctx.lineTo(width, points[points.length - 1][1]); // Hold the last value to 'now'
                    const right = points === segment ? width : points[points.length - 1][0];
                    ctx.setLineDash(series.dashed ? [4, 3] : []);
                    ctx.strokeStyle = color;
                    ctx.stroke();
                    ctx.setLineDash([]);
                    if (series.dashed)
                        continue;

                    ctx.lineTo(right, bottom);
                    ctx.lineTo(points[0][0], bottom);
                    ctx.closePath();
                    ctx.globalAlpha = 0.1;
                    ctx.fillStyle = color;
                    ctx.fill();
                    ctx.globalAlpha = 1;
                }
            }
        });
    }
    requestAnimationFrame(draw);
})();
</script>
</body>
</html>`;
}
