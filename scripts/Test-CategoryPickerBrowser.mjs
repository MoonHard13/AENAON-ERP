import assert from "node:assert/strict";
import { spawn, spawnSync } from "node:child_process";
import { createServer } from "node:http";
import { readFile, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

// Πραγματικό smoke test Chrome/CDP χωρίς νέα npm εξάρτηση.
const root = process.cwd();
const scripts = resolve(root, "src/Aenaon.Api/wwwroot/js");
const css = resolve(root, "src/Aenaon.Api/wwwroot/css");
const received = [];

const fixture = `<!doctype html>
<html lang="el">
<head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<link rel="stylesheet" href="/css/aenaon.css">
<link rel="stylesheet" href="/css/aenaon-backoffice.css"></head>
<body style="min-height:1100px;">
<h1 tabindex="-1">Ένας χώρος για κάθε επιχείρηση.</h1>
<div class="demo" style="position:fixed;left:35px;top:180px;width:min(930px,calc(100vw - 70px));">
<form method="post" action="/ui/catalog/save" class="catalog-editor__form">
<input type="hidden" name="tenantId" value="9698df79-3c31-41c9-b292-b7240c7265fb">
<input type="hidden" name="view" value="products">
<input type="hidden" name="codeMode" value="auto">
<label for="new-name">Περιγραφή *</label>
<input id="new-name" name="name" required value="">
<div class="catalog-editor__field catalog-editor__field--wide">
<div class="catalog-category-picker" data-catalog-picker
 data-tenant-id="9698df79-3c31-41c9-b292-b7240c7265fb" data-required="true">
<label class="catalog-category-picker__label" for="category-search">Κατηγορία *</label>
<div class="catalog-category-picker__control" data-picker-control>
 <span>⌕</span>
 <input id="category-search" type="search" data-picker-search role="combobox"
 aria-autocomplete="list" aria-haspopup="listbox" aria-expanded="false"
 aria-controls="category-options" maxlength="80" placeholder="Αναζήτηση κατηγορίας…">
 <button type="button" data-picker-toggle class="catalog-category-picker__toggle" tabindex="-1">⌄</button>
</div>
<div class="catalog-category-picker__popup" data-picker-popup hidden>
 <div id="category-options" class="catalog-category-picker__options"
  role="listbox" data-picker-options></div>
 <div class="catalog-category-picker__status" role="status"
  data-picker-status>Πληκτρολογήστε για αναζήτηση.</div>
 <button type="button" class="catalog-category-picker__create catalog-editor__quick-create"
  data-picker-create>+ Νέα κατηγορία</button>
</div>
<div class="catalog-category-picker__inline-action" data-picker-inline-action>
 <button type="button" class="catalog-category-picker__inline-create"
  data-picker-create>Δεν βρίσκετε την κατηγορία; Δημιουργήστε νέα</button>
</div>
<input type="hidden" name="categoryId" data-picker-value disabled>
<div class="catalog-category-picker__fallback" data-picker-fallback>
<select name="categoryId" data-picker-native required>
<option value="">Επιλέξτε κατηγορία</option><option value="00000000-0000-0000-0000-000000000003">3 — ΦΑΓΗΤΑ</option>
</select></div><p class="catalog-category-picker__error" data-picker-error hidden>Απαιτείται κατηγορία.</p>
</div></div>
<button type="submit" name="saveMode" value="close">Αποθήκευση</button>
<button hidden type="submit" data-category-quick-submit formnovalidate
 formaction="/ui/catalog/new-category" formmethod="post"></button>
</form></div>
<script src="/js/aenaon-category-picker.js" defer></script>
</body></html>`;

const categories = Array.from({ length: 30 }, (_, index) => ({
    id: "00000000-0000-0000-0000-" + String(index + 3).padStart(12, "0"),
    code: String(index + 3),
    name: index === 0 ? "ΦΑΓΗΤΑ" : "ΚΑΤΗΓΟΡΙΑ " + String(index + 1),
    path: index === 0 ? "ΦΑΓΗΤΑ" : "ΦΑΓΗΤΑ / ΚΑΤΗΓΟΡΙΑ " + String(index + 1),
    parentId: index === 0 ? null : "00000000-0000-0000-0000-000000000003"
}));

const server = createServer(async (request, response) => {
    const url = new URL(request.url || "/", "http://localhost");
    if (url.pathname === "/") {
        response.setHeader("Content-Type", "text/html; charset=utf-8");
        response.end(fixture);
        return;
    }
    if (url.pathname.startsWith("/js/") || url.pathname.startsWith("/css/")) {
        const isScript = url.pathname.startsWith("/js/");
        const name = url.pathname.slice(isScript ? 4 : 5);
        if (!/^[a-z0-9-]+\.(js|css)$/.test(name)) {
            response.writeHead(404).end();
            return;
        }
        response.setHeader("Content-Type", isScript ? "text/javascript" : "text/css");
        response.end(await readFile(join(isScript ? scripts : css, name)));
        return;
    }
    if (url.pathname === "/ui/catalog/categories") {
        response.setHeader("Content-Type", "application/json");
        const term = (url.searchParams.get("search") || "").trim().toLocaleLowerCase("el");
        const matches = categories.filter(category =>
            category.name.toLocaleLowerCase("el").startsWith(term) ||
            category.code.startsWith(term));
        response.end(JSON.stringify(matches));
        return;
    }
    if (url.pathname === "/ui/catalog/new-category" && request.method === "POST") {
        let body = "";
        for await (const chunk of request) {
            body += chunk.toString();
        }
        received.push(Object.fromEntries(new URLSearchParams(body)));
        response.setHeader("Content-Type", "text/html; charset=utf-8");
        response.end("<!doctype html><title>Νέα κατηγορία</title><h1>Η νέα κατηγορία άνοιξε.</h1>");
        return;
    }
    response.writeHead(404).end("Not found");
});

async function waitFor(predicate, message, tries = 90) {
    for (let attempt = 0; attempt < tries; attempt++) {
        const result = await predicate().catch(() => null);
        if (result) {
            return result;
        }
        await new Promise(done => setTimeout(done, 80));
    }
    throw new Error("Αποτυχία smoke test: " + message);
}

async function getFreePort() {
    const socket = createServer();
    await new Promise(done => socket.listen(0, "127.0.0.1", done));
    const port = socket.address().port;
    await new Promise(done => socket.close(done));
    return port;
}

class Cdp {
    constructor(socket) {
        this.socket = socket;
        this.sequence = 0;
        this.pending = new Map();
        socket.addEventListener("message", event => {
            const data = JSON.parse(event.data);
            if (!data.id || !this.pending.has(data.id)) {
                return;
            }
            const { resolve, reject } = this.pending.get(data.id);
            this.pending.delete(data.id);
            if (data.error) {
                reject(new Error(JSON.stringify(data.error)));
            } else {
                resolve(data.result || {});
            }
        });
    }

    command(method, params = {}) {
        return new Promise((resolve, reject) => {
            const id = ++this.sequence;
            this.pending.set(id, { resolve, reject });
            this.socket.send(JSON.stringify({ id, method, params }));
        });
    }

    async evaluate(expression) {
        const result = await this.command("Runtime.evaluate", {
            expression, returnByValue: true, awaitPromise: true
        });
        if (result.exceptionDetails) {
            throw new Error(JSON.stringify(result.exceptionDetails));
        }
        return result.result?.value;
    }
}

function browserExecutable() {
    const options = [process.env.CHROME_BIN, "google-chrome-stable",
        "google-chrome", "chromium", "chromium-browser"].filter(Boolean);
    for (const candidate of options) {
        if (spawnSync(candidate, ["--version"], { stdio: "ignore" }).status === 0) {
            return candidate;
        }
    }
    throw new Error("Δεν βρέθηκε Chromium/Chrome στο CI runner.");
}

let browser;
let websocket;
let userData;
try {
    await new Promise(done => server.listen(0, "127.0.0.1", done));
    const httpPort = server.address().port;
    const debugPort = await getFreePort();
    userData = await mkdtemp(join(tmpdir(), "aenaon-picker-chrome-"));

    browser = spawn(browserExecutable(), [
        "--headless=new", "--disable-gpu", "--no-sandbox",
        "--disable-dev-shm-usage", "--no-first-run",
        "--remote-allow-origins=*", "--remote-debugging-port=" + debugPort,
        "--user-data-dir=" + userData, "about:blank"
    ], { stdio: "ignore" });

    const page = await waitFor(async () => {
        const response = await fetch("http://127.0.0.1:" + debugPort + "/json/list");
        const targets = await response.json();
        return targets.find(item => item.type === "page");
    }, "Δεν ξεκίνησε το Chrome DevTools.");

    websocket = new WebSocket(page.webSocketDebuggerUrl);
    await new Promise((done, reject) => {
        websocket.addEventListener("open", done, { once: true });
        websocket.addEventListener("error", reject, { once: true });
    });
    const cdp = new Cdp(websocket);
    await cdp.command("Page.enable");
    await cdp.command("Runtime.enable");
    await cdp.command("Page.bringToFront");
    await cdp.command("Page.navigate", {
        url: "http://127.0.0.1:" + httpPort + "/"
    });
    await waitFor(() => cdp.evaluate(
        'document.querySelector("[data-catalog-picker]")?.classList.contains("catalog-category-picker--ready")'),
        "Δεν αρχικοποιήθηκε το πραγματικό JavaScript του Category Picker.");

    // Η προγραμματιστική εστίαση τίτλου δεν εμφανίζει πράσινο περίγραμμα.
    const headingOutline = await cdp.evaluate(`(() => {
        const heading = document.querySelector("h1");
        heading.focus();
        return getComputedStyle(heading).outlineStyle;
    })()`);
    assert.equal(headingOutline, "none");

    async function checkLayout(width, height, anchorTop) {
        await cdp.command("Emulation.setDeviceMetricsOverride", {
            width, height, deviceScaleFactor: 1, mobile: width < 600
        });
        const focusState = await cdp.evaluate(`(() => {
            const demo = document.querySelector(".demo");
            demo.style.top = "${anchorTop}px";
            const input = document.querySelector("[data-picker-search]");
            input.focus();
            return { focused:document.activeElement===input,
                hasFocus:document.hasFocus(),
                hidden:document.querySelector("[data-picker-popup]").hidden,
                ready:document.querySelector("[data-catalog-picker]").classList.contains("catalog-category-picker--ready") };
        })()`);
        process.stdout.write("Picker viewport " + width + "×" + height +
            ", top=" + anchorTop + ", focus=" + JSON.stringify(focusState) + "\\n");
        await waitFor(() => cdp.evaluate(
            '!document.querySelector("[data-picker-popup]").hidden'),
            "Δεν άνοιξε το picker.");

        const geometry = await cdp.evaluate(`(() => {
            const popup = document.querySelector("[data-picker-popup]");
            const create = popup.querySelector("[data-picker-create]");
            const inline = document.querySelector("[data-picker-inline-action]");
            const p = popup.getBoundingClientRect();
            const c = create.getBoundingClientRect();
            const i = inline.getBoundingClientRect();
            const control = document.querySelector("[data-picker-control]").getBoundingClientRect();
            return { popup: { top:p.top, bottom:p.bottom, left:p.left, right:p.right },
                create: { top:c.top, bottom:c.bottom }, inlineVisible:i.height>0,
                placement:popup.dataset.placement, controlTop:control.top,
                viewport: { width:innerWidth, height:innerHeight } };
        })()`);

        assert.ok(geometry.popup.top >= -2, JSON.stringify(geometry));
        assert.ok(geometry.popup.bottom <= geometry.viewport.height + 2,
            JSON.stringify(geometry));
        assert.ok(geometry.popup.left >= -2 && geometry.popup.right <= geometry.viewport.width + 2,
            JSON.stringify(geometry));
        assert.ok(geometry.create.top >= geometry.popup.top - 2 &&
            geometry.create.bottom <= geometry.popup.bottom + 2, JSON.stringify(geometry));
        assert.ok(geometry.inlineVisible, "Το μόνιμο κουμπί δημιουργίας δεν είναι ορατό.");
        if (anchorTop > height / 2) {
            assert.ok(["top", "viewport"].includes(geometry.placement),
                JSON.stringify(geometry));
        }
        await cdp.evaluate('document.querySelector("[data-picker-search]").blur()');
        return geometry;
    }

    await checkLayout(1366, 768, 120);
    await checkLayout(1366, 768, 610);
    await checkLayout(2560, 1380, 1110);
    await checkLayout(390, 640, 460);

    // Η επιλογή με το πληκτρολόγιο επιστρέφει GUID, όχι ελεύθερο κείμενο.
    await cdp.command("Emulation.setDeviceMetricsOverride", {
        width: 1366, height: 768, deviceScaleFactor: 1, mobile: false
    });
    const picked = await cdp.evaluate(`(() => {
        const input = document.querySelector("[data-picker-search]");
        input.focus();
        input.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowDown", bubbles: true }));
        input.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true }));
        return document.querySelector("[data-picker-value]").value;
    })()`);
    assert.equal(picked, categories[0].id);

    // Το click στο footer ελέγχεται ως πραγματική κίνηση ποντικιού μέσω Chrome CDP.
    const button = await cdp.evaluate(`(() => {
        const input = document.querySelector("[data-picker-search]");
        input.focus();
        const element = document.querySelector("[data-picker-popup] [data-picker-create]");
        const rect = element.getBoundingClientRect();
        return { x: rect.x + rect.width / 2, y: rect.y + rect.height / 2,
                 visible:rect.height > 0 };
    })()`);
    assert.ok(button.visible, "Το popup footer δεν είναι ορατό.");
    await cdp.command("Input.dispatchMouseEvent", {
        type: "mousePressed", x: button.x, y: button.y, button: "left", clickCount: 1
    });
    await cdp.command("Input.dispatchMouseEvent", {
        type: "mouseReleased", x: button.x, y: button.y, button: "left", clickCount: 1
    });

    await waitFor(async () => received.length > 0,
        "Το πραγματικό click δεν οδήγησε στο POST νέας κατηγορίας.");
    assert.equal(received.length, 1);
    assert.equal(received[0].view, "products");
    assert.equal(received[0].name, "");
    assert.equal(received[0].tenantId, "9698df79-3c31-41c9-b292-b7240c7265fb");

    process.stdout.write("Category Picker Chromium smoke: 4 viewports, focus, keyboard, real popup click + POST OK\n");
} finally {
    websocket?.close();
    browser?.kill("SIGKILL");
    await new Promise(done => server.close(done));
    if (userData) {
        await rm(userData, { recursive: true, force: true, maxRetries: 5, retryDelay: 120 });
    }
}
