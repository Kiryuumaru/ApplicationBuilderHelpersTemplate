// Trading Charts JS Interop for Lightweight Charts
// Uses TradingView's Lightweight Charts library

const charts = new Map();

export async function initializeChart(elementId, options) {
    const container = document.getElementById(elementId);
    if (!container) {
        console.error(`Container ${elementId} not found`);
        return null;
    }

    // Dynamic import of lightweight-charts from CDN
    if (!window.LightweightCharts) {
        await loadLightweightCharts();
    }

    const chartOptions = {
        layout: {
            background: { type: 'solid', color: options.backgroundColor || '#1e222d' },
            textColor: options.textColor || '#d1d4dc',
        },
        grid: {
            vertLines: { color: options.gridColor || '#2B2B43' },
            horzLines: { color: options.gridColor || '#2B2B43' },
        },
        crosshair: {
            mode: LightweightCharts.CrosshairMode.Normal,
        },
        rightPriceScale: {
            borderColor: options.borderColor || '#2B2B43',
        },
        timeScale: {
            borderColor: options.borderColor || '#2B2B43',
            timeVisible: true,
            secondsVisible: false,
        },
        width: container.clientWidth,
        height: options.height || 400,
    };

    const chart = LightweightCharts.createChart(container, chartOptions);

    // Add candlestick series
    const candlestickSeries = chart.addCandlestickSeries({
        upColor: options.upColor || '#26a69a',
        downColor: options.downColor || '#ef5350',
        borderDownColor: options.downColor || '#ef5350',
        borderUpColor: options.upColor || '#26a69a',
        wickDownColor: options.downColor || '#ef5350',
        wickUpColor: options.upColor || '#26a69a',
    });

    // Add volume series
    const volumeSeries = chart.addHistogramSeries({
        color: '#26a69a',
        priceFormat: {
            type: 'volume',
        },
        priceScaleId: '',
        scaleMargins: {
            top: 0.8,
            bottom: 0,
        },
    });

    const chartInstance = {
        chart,
        candlestickSeries,
        volumeSeries,
        markers: [],
    };

    charts.set(elementId, chartInstance);

    // Handle resize
    const resizeObserver = new ResizeObserver(entries => {
        for (const entry of entries) {
            chart.applyOptions({ width: entry.contentRect.width });
        }
    });
    resizeObserver.observe(container);

    return elementId;
}

export function updateCandlestickData(elementId, data) {
    const chartInstance = charts.get(elementId);
    if (!chartInstance) return;

    const formattedData = data.map(d => ({
        time: d.time,
        open: d.open,
        high: d.high,
        low: d.low,
        close: d.close,
    }));

    chartInstance.candlestickSeries.setData(formattedData);
}

export function updateVolumeData(elementId, data) {
    const chartInstance = charts.get(elementId);
    if (!chartInstance) return;

    const formattedData = data.map(d => ({
        time: d.time,
        value: d.volume,
        color: d.close >= d.open ? '#26a69a80' : '#ef535080',
    }));

    chartInstance.volumeSeries.setData(formattedData);
}

export function addCandlestick(elementId, candle) {
    const chartInstance = charts.get(elementId);
    if (!chartInstance) return;

    chartInstance.candlestickSeries.update({
        time: candle.time,
        open: candle.open,
        high: candle.high,
        low: candle.low,
        close: candle.close,
    });

    chartInstance.volumeSeries.update({
        time: candle.time,
        value: candle.volume,
        color: candle.close >= candle.open ? '#26a69a80' : '#ef535080',
    });
}

export function addMarker(elementId, marker) {
    const chartInstance = charts.get(elementId);
    if (!chartInstance) return;

    chartInstance.markers.push({
        time: marker.time,
        position: marker.position || 'belowBar',
        color: marker.color || '#2196F3',
        shape: marker.shape || 'arrowUp',
        text: marker.text || '',
    });

    chartInstance.candlestickSeries.setMarkers(chartInstance.markers);
}

export function clearMarkers(elementId) {
    const chartInstance = charts.get(elementId);
    if (!chartInstance) return;

    chartInstance.markers = [];
    chartInstance.candlestickSeries.setMarkers([]);
}

export function addPriceLine(elementId, price, options) {
    const chartInstance = charts.get(elementId);
    if (!chartInstance) return;

    return chartInstance.candlestickSeries.createPriceLine({
        price: price,
        color: options.color || '#be1238',
        lineWidth: options.lineWidth || 2,
        lineStyle: options.lineStyle || LightweightCharts.LineStyle.Solid,
        axisLabelVisible: true,
        title: options.title || '',
    });
}

export function fitContent(elementId) {
    const chartInstance = charts.get(elementId);
    if (!chartInstance) return;

    chartInstance.chart.timeScale().fitContent();
}

export function scrollToRealTime(elementId) {
    const chartInstance = charts.get(elementId);
    if (!chartInstance) return;

    chartInstance.chart.timeScale().scrollToRealTime();
}

export function disposeChart(elementId) {
    const chartInstance = charts.get(elementId);
    if (!chartInstance) return;

    chartInstance.chart.remove();
    charts.delete(elementId);
}

async function loadLightweightCharts() {
    return new Promise((resolve, reject) => {
        if (window.LightweightCharts) {
            resolve();
            return;
        }

        const script = document.createElement('script');
        script.src = 'https://unpkg.com/lightweight-charts@4.1.0/dist/lightweight-charts.standalone.production.js';
        script.onload = () => resolve();
        script.onerror = () => reject(new Error('Failed to load Lightweight Charts'));
        document.head.appendChild(script);
    });
}

// Order Book visualization
const orderBooks = new Map();

export function initializeOrderBook(elementId, options) {
    const container = document.getElementById(elementId);
    if (!container) return null;

    const canvas = document.createElement('canvas');
    canvas.width = container.clientWidth;
    canvas.height = options.height || 300;
    container.appendChild(canvas);

    const ctx = canvas.getContext('2d');
    const orderBook = {
        canvas,
        ctx,
        bids: [],
        asks: [],
        options: {
            bidColor: options.bidColor || 'rgba(38, 166, 154, 0.4)',
            askColor: options.askColor || 'rgba(239, 83, 80, 0.4)',
            bidLineColor: options.bidLineColor || '#26a69a',
            askLineColor: options.askLineColor || '#ef5350',
            textColor: options.textColor || '#d1d4dc',
            backgroundColor: options.backgroundColor || '#1e222d',
        }
    };

    orderBooks.set(elementId, orderBook);

    // Handle resize
    const resizeObserver = new ResizeObserver(entries => {
        for (const entry of entries) {
            canvas.width = entry.contentRect.width;
            renderOrderBook(elementId);
        }
    });
    resizeObserver.observe(container);

    return elementId;
}

export function updateOrderBook(elementId, bids, asks) {
    const orderBook = orderBooks.get(elementId);
    if (!orderBook) return;

    orderBook.bids = bids;
    orderBook.asks = asks;
    renderOrderBook(elementId);
}

function renderOrderBook(elementId) {
    const orderBook = orderBooks.get(elementId);
    if (!orderBook) return;

    const { canvas, ctx, bids, asks, options } = orderBook;
    const width = canvas.width;
    const height = canvas.height;

    // Clear
    ctx.fillStyle = options.backgroundColor;
    ctx.fillRect(0, 0, width, height);

    if (bids.length === 0 && asks.length === 0) return;

    // Calculate cumulative volumes
    let bidsCumulative = [];
    let asksCumulative = [];
    let cumVol = 0;

    for (const bid of bids) {
        cumVol += bid.quantity;
        bidsCumulative.push({ price: bid.price, cumVolume: cumVol });
    }

    cumVol = 0;
    for (const ask of asks) {
        cumVol += ask.quantity;
        asksCumulative.push({ price: ask.price, cumVolume: cumVol });
    }

    const maxVolume = Math.max(
        bidsCumulative.length > 0 ? bidsCumulative[bidsCumulative.length - 1].cumVolume : 0,
        asksCumulative.length > 0 ? asksCumulative[asksCumulative.length - 1].cumVolume : 0
    );

    if (maxVolume === 0) return;

    const midPrice = bids.length > 0 && asks.length > 0
        ? (bids[0].price + asks[0].price) / 2
        : bids[0]?.price || asks[0]?.price || 0;

    const priceRange = Math.max(
        bids.length > 0 ? Math.abs(bids[bids.length - 1].price - midPrice) : 0,
        asks.length > 0 ? Math.abs(asks[asks.length - 1].price - midPrice) : 0
    ) * 1.1;

    const centerX = width / 2;
    const scaleX = centerX / priceRange;
    const scaleY = height / maxVolume;

    // Draw bids (left side)
    ctx.beginPath();
    ctx.moveTo(centerX, height);
    for (const point of bidsCumulative) {
        const x = centerX - (midPrice - point.price) * scaleX;
        const y = height - point.cumVolume * scaleY;
        ctx.lineTo(x, y);
    }
    ctx.lineTo(0, height);
    ctx.closePath();
    ctx.fillStyle = options.bidColor;
    ctx.fill();
    ctx.strokeStyle = options.bidLineColor;
    ctx.stroke();

    // Draw asks (right side)
    ctx.beginPath();
    ctx.moveTo(centerX, height);
    for (const point of asksCumulative) {
        const x = centerX + (point.price - midPrice) * scaleX;
        const y = height - point.cumVolume * scaleY;
        ctx.lineTo(x, y);
    }
    ctx.lineTo(width, height);
    ctx.closePath();
    ctx.fillStyle = options.askColor;
    ctx.fill();
    ctx.strokeStyle = options.askLineColor;
    ctx.stroke();

    // Draw mid price line
    ctx.beginPath();
    ctx.moveTo(centerX, 0);
    ctx.lineTo(centerX, height);
    ctx.strokeStyle = '#666';
    ctx.setLineDash([5, 5]);
    ctx.stroke();
    ctx.setLineDash([]);

    // Draw labels
    ctx.fillStyle = options.textColor;
    ctx.font = '12px sans-serif';
    ctx.textAlign = 'center';
    ctx.fillText(midPrice.toFixed(2), centerX, 20);

    if (bids.length > 0) {
        ctx.textAlign = 'left';
        ctx.fillText(`Bid: ${bids[0].price.toFixed(2)}`, 10, 20);
    }
    if (asks.length > 0) {
        ctx.textAlign = 'right';
        ctx.fillText(`Ask: ${asks[0].price.toFixed(2)}`, width - 10, 20);
    }
}

export function disposeOrderBook(elementId) {
    const orderBook = orderBooks.get(elementId);
    if (!orderBook) return;

    orderBook.canvas.remove();
    orderBooks.delete(elementId);
}
