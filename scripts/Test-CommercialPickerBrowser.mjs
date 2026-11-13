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
const fixture = "<!doctype html>\n<html lang=\"el\">\n<head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n<link rel=\"stylesheet\" href=\"/css/aenaon.css\">\n<link rel=\"stylesheet\" href=\"/css/aenaon-backoffice.css\"></head>\n<body style=\"min-height:1100px\">\n<div class=\"demo\" style=\"position:fixed;left:35px;top:130px;width:min(850px,calc(100vw - 70px));\">\n<form class=\"catalog-editor__form\" method=\"post\" action=\"/ui/catalog/save\">\n<div class=\"catalog-editor__grid\">\n<div class=\"catalog-editor__field\">\n<div class=\"catalog-category-picker catalog-commercial-picker\" data-commercial-picker data-required=\"true\">\n<label class=\"catalog-category-picker__label\" for=\"edit-unit\">Μονάδα μέτρησης *</label>\n<div class=\"catalog-category-picker__control\" data-commercial-control>\n<span class=\"app-icon\">⌕</span>\n<input id=\"edit-unit\" data-commercial-search type=\"search\" role=\"combobox\" aria-autocomplete=\"list\"\n aria-haspopup=\"listbox\" aria-expanded=\"false\" aria-controls=\"edit-unit-options\"\n aria-describedby=\"edit-unit-status edit-unit-error\" aria-required=\"true\" autocomplete=\"off\">\n<button class=\"catalog-category-picker__toggle\" type=\"button\" data-commercial-toggle tabindex=\"-1\">⌄</button>\n</div>\n<div class=\"catalog-category-picker__popup\" data-commercial-popup hidden>\n<div id=\"edit-unit-options\" class=\"catalog-category-picker__options\" role=\"listbox\"\n aria-label=\"Μονάδα μέτρησης\" data-commercial-options></div>\n<div id=\"edit-unit-status\" class=\"catalog-category-picker__status\" data-commercial-status role=\"status\">Επιλογές</div>\n</div>\n<input type=\"hidden\" name=\"unitOfMeasure\" value=\"1\" data-commercial-value disabled>\n<div class=\"catalog-category-picker__fallback\" data-commercial-fallback>\n<select name=\"unitOfMeasure\" data-commercial-native required><option value=\"\" disabled>Επιλέξτε</option>\n<option value=\"1\" selected>Τεμάχιο (τεμ.)</option>\n<option value=\"2\">Κιλό (kg)</option>\n<option value=\"3\">Γραμμάριο (g)</option>\n<option value=\"7\">Ώρα</option>\n</select></div>\n<p id=\"edit-unit-error\" class=\"catalog-category-picker__error\" data-commercial-error hidden>Απαιτείται έγκυρη τιμή.</p>\n</div></div>\n<div class=\"catalog-editor__field\">\n<div class=\"catalog-category-picker catalog-commercial-picker\" data-commercial-picker data-required=\"true\">\n<label class=\"catalog-category-picker__label\" for=\"edit-accounting\">Λογιστικός τύπος *</label>\n<div class=\"catalog-category-picker__control\" data-commercial-control>\n<span class=\"app-icon\">⌕</span>\n<input id=\"edit-accounting\" data-commercial-search type=\"search\" role=\"combobox\" aria-autocomplete=\"list\"\n aria-haspopup=\"listbox\" aria-expanded=\"false\" aria-controls=\"edit-accounting-options\"\n aria-describedby=\"edit-accounting-status edit-accounting-error\" aria-required=\"true\" autocomplete=\"off\">\n<button class=\"catalog-category-picker__toggle\" type=\"button\" data-commercial-toggle tabindex=\"-1\">⌄</button>\n</div>\n<div class=\"catalog-category-picker__popup\" data-commercial-popup hidden>\n<div id=\"edit-accounting-options\" class=\"catalog-category-picker__options\" role=\"listbox\"\n aria-label=\"Λογιστικός τύπος\" data-commercial-options></div>\n<div id=\"edit-accounting-status\" class=\"catalog-category-picker__status\" data-commercial-status role=\"status\">Επιλογές</div>\n</div>\n<input type=\"hidden\" name=\"accountingType\" value=\"1\" data-commercial-value disabled>\n<div class=\"catalog-category-picker__fallback\" data-commercial-fallback>\n<select name=\"accountingType\" data-commercial-native required><option value=\"\" disabled>Επιλέξτε</option>\n<option value=\"1\" selected>Εμπόρευμα</option>\n<option value=\"2\">Προϊόν παραγωγής</option>\n<option value=\"3\">Πρώτη ύλη</option>\n<option value=\"4\">Αναλώσιμο</option>\n<option value=\"6\">Πάγιο</option>\n</select></div>\n<p id=\"edit-accounting-error\" class=\"catalog-category-picker__error\" data-commercial-error hidden>Απαιτείται έγκυρη τιμή.</p>\n</div></div>\n</div>\n<button type=\"submit\">Αποθήκευση</button></form></div>\n<script src=\"/js/aenaon-commercial-picker.js\" defer></script>\n</body></html>";
const received = [];
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
    if (url.pathname === "/ui/catalog/save" && request.method === "POST") {
        let body = "";
        for await (const chunk of request) {
            body += chunk.toString();
        }
        const fields = new URLSearchParams(body);
        received.push({
            unit: fields.getAll("unitOfMeasure"),
            accounting: fields.getAll("accountingType")
        });
        response.setHeader("Content-Type", "text/html; charset=utf-8");
        response.end("<!doctype html><title>Αποθηκεύτηκε</title><h1>OK</h1>");
        return;
    }
    response.writeHead(404).end("Not found");
});

async function waitFor(predicate, message, tries = 90) {
    for (let attempt = 0; attempt < tries; attempt++) {
        let result = null;
        try {
            result = await predicate();
        } catch {
            // Κατά τη φόρτωση μπορεί να μην είναι ακόμη διαθέσιμο το DOM.
        }
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

    // Πρώτα αποδεικνύεται η λειτουργία του native select χωρίς καθόλου JavaScript.
    await cdp.command("Emulation.setScriptExecutionDisabled", { value: true });
    await cdp.command("Page.navigate", { url: "http://127.0.0.1:" + httpPort + "/" });
    const native = await waitFor(() => cdp.evaluate(`(() => {
        const select = document.querySelector("[data-commercial-native]");
        const hidden = document.querySelector("[data-commercial-value]");
        return select && { active:!select.disabled, hiddenDisabled:hidden.disabled,
            ready:document.querySelector("[data-commercial-picker]").classList.contains("catalog-category-picker--ready") };
    })()`), "Δεν εμφανίστηκε το native fallback.");
    assert.deepEqual(native, { active: true, hiddenDisabled: true, ready: false });

    await cdp.command("Emulation.setScriptExecutionDisabled", { value: false });
    await cdp.command("Page.navigate", { url: "http://127.0.0.1:" + httpPort + "/" });
    await waitFor(() => cdp.evaluate(
        'document.querySelectorAll(".catalog-commercial-picker.catalog-category-picker--ready").length===2'),
        "Δεν ενεργοποιήθηκαν οι δύο εμπορικοί επιλογείς.");

    // Με και χωρίς JavaScript κάθε πεδίο έχει ακριβώς μία ενεργή φόρμα.
    let fields = await cdp.evaluate(`(() => ({
        unit: new FormData(document.querySelector("form")).getAll("unitOfMeasure"),
        accounting: new FormData(document.querySelector("form")).getAll("accountingType"),
        disabled: [...document.querySelectorAll("[data-commercial-native]")].every(x=>x.disabled),
        inputs: [...document.querySelectorAll("[data-commercial-value]")].every(x=>!x.disabled)
    }))()`);
    assert.deepEqual(fields, { unit:["1"], accounting:["1"], disabled:true, inputs:true });

    async function geometry(width, height, anchor) {
        await cdp.command("Emulation.setDeviceMetricsOverride", {
            width, height, deviceScaleFactor: 1, mobile: width < 600
        });
        const result = await cdp.evaluate(`(() => {
            document.querySelector(".demo").style.top="${anchor}px";
            const input=document.querySelector("[data-commercial-search]");
            input.focus();
            input.click();
            const popup=document.querySelector("[data-commercial-popup]");
            const rect=popup.getBoundingClientRect();
            const control=document.querySelector("[data-commercial-control]").getBoundingClientRect();
            return { visible:!popup.hidden, placement:popup.dataset.placement,
                top:rect.top,bottom:rect.bottom,left:rect.left,right:rect.right,
                width:control.width,height:control.height,aria:input.getAttribute("aria-expanded"),
                focusOutline:getComputedStyle(document.querySelector("[data-commercial-control]")).outlineStyle,
                viewportWidth:innerWidth,viewportHeight:innerHeight };
        })()`);
        assert.equal(result.visible, true);
        assert.equal(result.aria, "true");
        assert.ok(result.height >= 50, JSON.stringify(result));
        assert.ok(result.top >= -2 && result.bottom <= height + 2, JSON.stringify(result));
        assert.ok(result.left >= -2 && result.right <= width + 2, JSON.stringify(result));
        if (anchor > height/2) {
            assert.ok(["top","viewport"].includes(result.placement), JSON.stringify(result));
        }
        await cdp.evaluate('document.querySelector("[data-commercial-search]").blur()');
        return result;
    }
    await geometry(1366,768,120);
    await geometry(1366,768,610);
    await geometry(2560,1380,1110);
    await geometry(390,640,460);

    await cdp.command("Emulation.setDeviceMetricsOverride", {
        width:1366,height:768,deviceScaleFactor:1,mobile:false
    });
    const keyboard = await cdp.evaluate(`(() => {
        document.querySelector(".demo").style.top="120px";
        const input=document.querySelector("#edit-unit");
        input.focus();
        input.value="Κιλό";
        input.dispatchEvent(new Event("input",{bubbles:true}));
        const before=document.querySelector('[name="unitOfMeasure"][data-commercial-value]').value;
        input.dispatchEvent(new KeyboardEvent("keydown",{key:"ArrowDown",bubbles:true}));
        input.dispatchEvent(new KeyboardEvent("keydown",{key:"Enter",bubbles:true}));
        const selected=document.querySelector('[name="unitOfMeasure"][data-commercial-value]').value;
        input.click();
        input.value="Γραμμάριο";
        input.dispatchEvent(new Event("input",{bubbles:true}));
        input.dispatchEvent(new KeyboardEvent("keydown",{key:"Escape",bubbles:true}));
        const restored=document.querySelector('[name="unitOfMeasure"][data-commercial-value]').value;
        input.click();
        input.dispatchEvent(new KeyboardEvent("keydown",{key:"Tab",bubbles:true}));
        return {before,selected,restored,closed:document.querySelector("[data-commercial-popup]").hidden,
            formValues:new FormData(document.querySelector("form")).getAll("unitOfMeasure")};
    })()`);
    assert.deepEqual(keyboard,{before:"",selected:"2",restored:"2",closed:true,formValues:["2"]});

    // Πραγματικό click ποντικιού: επιλογή «Πρώτη ύλη» και όχι απλό synthetic event.
    const location=await cdp.evaluate(`(() => {
        const input=document.querySelector("#edit-accounting");
        input.focus();
        const option=document.querySelector('[data-commercial-options] [data-commercial-index="2"]');
        const rect=option.getBoundingClientRect();
        return {x:rect.left+rect.width/2,y:rect.top+rect.height/2,
            visible:rect.height>0,placement:option.closest("[data-commercial-popup]").dataset.placement};
    })()`);
    assert.ok(location.visible,JSON.stringify(location));
    await cdp.command("Input.dispatchMouseEvent",{
        type:"mousePressed",x:location.x,y:location.y,button:"left",clickCount:1
    });
    await cdp.command("Input.dispatchMouseEvent",{
        type:"mouseReleased",x:location.x,y:location.y,button:"left",clickCount:1
    });
    fields = await cdp.evaluate(`(() => ({
        unit:new FormData(document.querySelector("form")).getAll("unitOfMeasure"),
        accounting:new FormData(document.querySelector("form")).getAll("accountingType"),
        accountingLabel:document.querySelector("#edit-accounting").value
    }))()`);
    assert.deepEqual(fields,{unit:["2"],accounting:["3"],accountingLabel:"Πρώτη ύλη"});

    // Το χειροκίνητο κείμενο δεν περνάει ως enum και μπλοκάρει το submit.
    const invalid=await cdp.evaluate(`(() => {
        const input=document.querySelector("#edit-unit");
        input.focus();
        input.value="αυθαίρετη τιμή";
        input.dispatchEvent(new Event("input",{bubbles:true}));
        document.querySelector('button[type="submit"]').click();
        return {invalid:input.getAttribute("aria-invalid"),
            empty:document.querySelector('[name="unitOfMeasure"][data-commercial-value]').value};
    })()`);
    assert.deepEqual(invalid,{invalid:"true",empty:""});
    assert.equal(received.length,0);

    // Escape επαναφέρει την τελευταία πραγματική επιλογή και η φόρμα στέλνει μόνο αριθμούς.
    await cdp.evaluate(`(() => {
        const input=document.querySelector("#edit-unit");
        input.dispatchEvent(new KeyboardEvent("keydown",{key:"Escape",bubbles:true}));
        document.querySelector('button[type="submit"]').click();
    })()`);
    await waitFor(() => received.length === 1, "Δεν πραγματοποιήθηκε έγκυρη αποθήκευση.");
    assert.deepEqual(received[0],{unit:["2"],accounting:["3"]});
    process.stdout.write("Commercial Picker Chromium smoke: native fallback, 4 viewports, keyboard, ARIA, mouse and numeric POST OK\\n");
} finally {
    websocket?.close();
    browser?.kill("SIGKILL");
    await new Promise(done => server.close(done));
    if (userData) {
        await rm(userData, { recursive: true, force: true, maxRetries: 5, retryDelay: 120 });
    }
}
