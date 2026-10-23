// Utilidades comunes para las pantallas de Ventas y Compras (carrito en memoria).

function ssGetAntiForgeryToken() {
    const input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
}

function ssFormatMoney(value) {
    return '$' + Number(value || 0).toFixed(2);
}

async function ssPostJson(url, payload) {
    const response = await fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': ssGetAntiForgeryToken()
        },
        body: JSON.stringify(payload)
    });
    const data = await response.json().catch(() => ({}));
    return { ok: response.ok, data };
}

function ssDebounce(fn, delay) {
    let timer;
    return (...args) => {
        clearTimeout(timer);
        timer = setTimeout(() => fn(...args), delay);
    };
}

// Escapa texto antes de insertarlo con innerHTML (nombres de productos, respuestas de la IA, etc.),
// para evitar inyección de HTML/JS (XSS).
function ssEscape(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}
