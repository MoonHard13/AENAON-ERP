// Ελάχιστη ενίσχυση προσιτότητας του στατικού Blazor UI· η πιστοποίηση γίνεται μόνο στον server.
(() => {
    "use strict";

    const storageKey = "aenaon-theme";
    const root = document.documentElement;

    function saveTheme(value) {
        try {
            window.localStorage.setItem(storageKey, value);
        } catch {
            // Η εφαρμογή παραμένει λειτουργική ακόμη και όταν απαγορεύεται η τοπική αποθήκευση.
        }
    }

    function currentTheme() {
        return root.dataset.theme || (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
    }

    function updateThemeLabels() {
        const nextName = currentTheme() === "dark" ? "Φωτεινό θέμα" : "Σκούρο θέμα";
        document.querySelectorAll("[data-theme-label]").forEach(element => {
            element.textContent = nextName;
            element.closest("[data-theme-toggle]")?.setAttribute("aria-label", "Ενεργοποίηση: " + nextName);
        });
    }

    function toggleTheme() {
        const nextTheme = currentTheme() === "dark" ? "light" : "dark";
        root.dataset.theme = nextTheme;
        saveTheme(nextTheme);
        updateThemeLabels();
    }

    // Ανάθεση χειριστών στο document για στοιχεία που αντικαθιστά το Blazor streaming SSR.
    document.addEventListener("click", event => {
        if (!(event.target instanceof Element)) {
            return;
        }

        if (event.target.closest("[data-theme-toggle]")) {
            toggleTheme();
            return;
        }

        const button = event.target.closest("[data-password-toggle]");
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        const targetId = button.getAttribute("aria-controls");
        const field = targetId ? document.getElementById(targetId) : null;
        if (!(field instanceof HTMLInputElement)) {
            return;
        }

        const show = field.type === "password";
        field.type = show ? "text" : "password";
        button.textContent = show ? "Απόκρυψη" : "Εμφάνιση";
        button.setAttribute("aria-label", show ? "Απόκρυψη κωδικού" : "Εμφάνιση κωδικού");
        field.focus({ preventScroll: true });
    });

    // Το Blazor ανανεώνει τους τίτλους/menus όταν το streaming αντικαθιστά HTML.
    if (window.Blazor && typeof window.Blazor.addEventListener === "function") {
        window.Blazor.addEventListener("enhancedload", updateThemeLabels);
    }

    document.addEventListener("DOMContentLoaded", () => {
        updateThemeLabels();

        // Οι δυναμικές αντικαταστάσεις του streaming SSR δεν απαιτούν επανασύνδεση listeners.
        // Η αρχικοποίηση του login form παραμένει τοπική στη στατική σελίδα.
        const loginForm = document.querySelector(".login-form");
        if (loginForm) {
            const button = loginForm.querySelector('button[type="submit"]');
            const originalText = button?.innerHTML;
            let recoveryTimer;

            function restoreSubmit() {
                clearTimeout(recoveryTimer);
                if (!button) {
                    return;
                }

                button.disabled = false;
                button.removeAttribute("aria-busy");
                button.innerHTML = originalText;
            }

            loginForm.addEventListener("submit", () => {
                if (!button) {
                    return;
                }

                button.disabled = true;
                button.setAttribute("aria-busy", "true");
                button.textContent = "Γίνεται σύνδεση…";
                // Επιτρέπουμε νέα προσπάθεια αν το HTTP αίτημα δεν ολοκληρωθεί.
                recoveryTimer = window.setTimeout(restoreSubmit, 15000);
            });
            window.addEventListener("pageshow", restoreSubmit);
        }
    });

    // Επιλογή χειροκίνητου/αυτόματου κωδικού χωρίς ψευδή δέσμευση αριθμού στον browser.
    document.addEventListener("change", event => {
        if (!(event.target instanceof HTMLInputElement) ||
            !event.target.matches("[data-code-mode]")) {
            return;
        }

        const input = document.getElementById("edit-code");
        if (!(input instanceof HTMLInputElement)) {
            return;
        }

        const auto = event.target.value === "auto";
        input.disabled = auto;
        input.required = !auto;
        document.querySelectorAll("[data-auto-code-hint]").forEach(element => {
            element.hidden = !auto;
        });
        document.querySelectorAll("[data-manual-code-hint]").forEach(element => {
            element.hidden = auto;
        });
        if (!auto) {
            input.focus({ preventScroll: true });
        }
    });

    // Οι επιλογές κατηγορίας ανακτώνται τμηματικά από τον εξουσιοδοτημένο server.
    // Δεν φορτώνεται ολόκληρο το δέντρο στον browser.
    const lookupDelays = new WeakMap();
    document.addEventListener("input", event => {
        if (!(event.target instanceof HTMLInputElement) ||
            !event.target.matches("[data-catalog-category-search]")) {
            return;
        }

        const search = event.target;
        const selector = document.querySelector("[data-catalog-category-select]");
        if (!(selector instanceof HTMLSelectElement)) {
            return;
        }

        window.clearTimeout(lookupDelays.get(search));
        const handle = window.setTimeout(async () => {
            const tenantId = search.dataset.tenantId;
            if (!tenantId) {
                return;
            }

            const selectedId = selector.value || search.dataset.selectedId || "";
            const params = new URLSearchParams({ tenantId, search: search.value });
            if (selectedId) {
                params.set("selectedId", selectedId);
            }

            selector.setAttribute("aria-busy", "true");
            try {
                const response = await window.fetch(
                    "/ui/catalog/categories?" + params.toString(), {
                        method: "GET",
                        credentials: "same-origin",
                        headers: { "Accept": "application/json" }
                    });
                if (!response.ok) {
                    throw new Error("Catalog category search failed");
                }

                const categories = await response.json();
                const preserved = selector.value;
                const rootLabel = selector.querySelector('option[value=""]')?.textContent || "";
                selector.replaceChildren();
                const empty = document.createElement("option");
                empty.value = "";
                empty.textContent = rootLabel;
                selector.appendChild(empty);
                for (const category of categories) {
                    const option = document.createElement("option");
                    option.value = category.id;
                    option.textContent = category.code + " — " + (category.path || category.name);
                    selector.appendChild(option);
                }

                selector.value = preserved;
                if (!selector.value && preserved) {
                    selector.value = "";
                }
            } catch {
                // Διατηρούμε τις προηγούμενες επιλογές όταν αποτυγχάνει το δίκτυο.
                search.setAttribute("aria-invalid", "true");
            } finally {
                selector.removeAttribute("aria-busy");
            }
        }, 260);
        lookupDelays.set(search, handle);
    });

    // Διπλή υποβολή εμποδίζεται στη φόρμα· η πραγματική προστασία είναι το requestId της SQL.
    document.addEventListener("submit", event => {
        if (!(event.target instanceof HTMLFormElement) ||
            !event.target.matches(".catalog-editor__form")) {
            return;
        }

        // Το κουμπί υποβολής μεταφέρει αυτόματα το saveMode.
        // Δεν προσθέτουμε δεύτερο hidden input με το ίδιο όνομα, γιατί ο server
        // θα λάμβανε διπλή παράμετρο και η καταχώρηση θα απορριπτόταν.
        const form = event.target;
        form.setAttribute("aria-busy", "true");
        window.setTimeout(() => {
            form.querySelectorAll('button[type="submit"]').forEach(button => {
                button.disabled = true;
            });
        }, 0);
    });

    window.addEventListener("pageshow", () => {
        document.querySelectorAll(".catalog-editor__form").forEach(form => {
            form.removeAttribute("aria-busy");
            form.querySelectorAll('button[type="submit"]').forEach(button => {
                button.disabled = false;
            });
        });
    });

    // Το δέντρο ανακτά κάθε επόμενο επίπεδο μόνο κατά την ανάπτυξή του.
    // Το περιεχόμενο εισάγεται με textContent για ασφαλή HTML απόδοση.
    document.addEventListener("click", async event => {
        if (!(event.target instanceof Element)) {
            return;
        }

        const button = event.target.closest("[data-category-branch]");
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        const parentId = button.dataset.categoryBranch;
        const tenantId = button.dataset.tenantId;
        const target = button.nextElementSibling;
        if (!(target instanceof HTMLElement) || !parentId || !tenantId) {
            return;
        }

        const expanded = button.getAttribute("aria-expanded") === "true";
        if (expanded) {
            button.setAttribute("aria-expanded", "false");
            button.setAttribute("aria-label", "Ανάπτυξη υποκατηγοριών");
            target.hidden = true;
            return;
        }

        button.setAttribute("aria-expanded", "true");
        button.setAttribute("aria-label", "Σύμπτυξη υποκατηγοριών");
        target.hidden = false;
        if (target.dataset.loaded === "true") {
            return;
        }

        button.disabled = true;
        target.textContent = "Φόρτωση υποκατηγοριών…";
        try {
            const params = new URLSearchParams({ tenantId, parentId });
            const response = await window.fetch(
                "/ui/catalog/children?" + params.toString(), {
                    method: "GET", credentials: "same-origin",
                    headers: { "Accept": "application/json" }
                });
            if (!response.ok) {
                throw new Error("Category branch request denied");
            }

            const page = await response.json();
            const list = document.createElement("ul");
            list.className = "catalog-tree-list";
            for (const category of page.rows || []) {
                const item = document.createElement("li");
                const line = document.createElement("div");
                line.className = "catalog-tree-line";
                const code = document.createElement("strong");
                code.textContent = category.code;
                const name = document.createElement("span");
                name.textContent = category.name;
                line.append(code, name);
                item.appendChild(line);

                if (category.hasChildren) {
                    const toggle = document.createElement("button");
                    toggle.type = "button";
                    toggle.className = "catalog-tree-toggle";
                    toggle.dataset.categoryBranch = category.id;
                    toggle.dataset.tenantId = tenantId;
                    toggle.setAttribute("aria-expanded", "false");
                    const targetId = "nested-" + parentId + "-" + category.id;
                    toggle.setAttribute("aria-controls", targetId);
                    toggle.textContent = "Ανάπτυξη υποκατηγοριών";
                    const subtree = document.createElement("div");
                    subtree.id = targetId;
                    subtree.className = "catalog-tree-children";
                    subtree.hidden = true;
                    item.append(toggle, subtree);
                }

                list.appendChild(item);
            }

            target.replaceChildren(list);
            if (page.hasMore) {
                const more = document.createElement("a");
                more.className = "catalog-edit-link";
                more.href = "/catalog?" + new URLSearchParams({
                    view: "categories", tenantId, parentId
                }).toString();
                more.textContent = "Προβολή όλων των υποκατηγοριών";
                target.appendChild(more);
            }

            if (!list.children.length) {
                target.textContent = "Δεν υπάρχουν υποκατηγορίες.";
            }

            target.dataset.loaded = "true";
        } catch {
            target.textContent = "Αποτυχία φόρτωσης. Δοκιμάστε ξανά.";
            button.setAttribute("aria-expanded", "false");
            target.hidden = true;
        } finally {
            button.disabled = false;
        }
    });

    // Επιβεβαίωση απενεργοποίησης/ενεργοποίησης χωρίς inline scripts.
    document.addEventListener("submit", event => {
        if (event.target instanceof HTMLFormElement &&
            event.target.matches(".catalog-state-form") &&
            !window.confirm("Επιβεβαιώνετε την αλλαγή κατάστασης;")) {
            event.preventDefault();
        }
    });

    // Κλείνουμε τα αναδυόμενα μενού με Escape ή κλικ έξω από αυτά.
    document.addEventListener("pointerdown", event => {
        document.querySelectorAll("details.user-menu[open], details.mobile-navigation[open]")
            .forEach(menu => {
                if (!menu.contains(event.target)) {
                    menu.open = false;
                }
            });
    });

    document.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            document.querySelectorAll("details.user-menu[open], details.mobile-navigation[open]")
                .forEach(menu => {
                    menu.open = false;
                    menu.querySelector("summary")?.focus({ preventScroll: true });
                });
            return;
        }

        if (!event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) {
            return;
        }

        switch (event.key.toLowerCase()) {
            case "t":
                event.preventDefault();
                toggleTheme();
                break;
            case "l":
                event.preventDefault();
                window.location.assign("/login");
                break;
            case "d":
                event.preventDefault();
                window.location.assign("/dashboard");
                break;
            case "w": {
                const workspaceLink = document.querySelector(".workspace-indicator");
                if (workspaceLink instanceof HTMLAnchorElement) {
                    event.preventDefault();
                    window.location.assign(workspaceLink.href);
                }
                break;
            }
            case "c":
                event.preventDefault();
                window.location.assign("/catalog");
                break;
            case "k": {
                // Προστατευμένη γρήγορη μετάβαση σε νέα κατηγορία από την ανοιχτή φόρμα.
                const quickCreate = document.querySelector(".catalog-editor__quick-create");
                const editor = document.querySelector(".catalog-editor__form");
                if (quickCreate instanceof HTMLButtonElement &&
                    editor instanceof HTMLFormElement) {
                    event.preventDefault();
                    editor.requestSubmit(quickCreate);
                }
                break;
            }
            case "n": {
                const newLink = document.querySelector(".catalog-create");
                if (newLink instanceof HTMLAnchorElement) {
                    event.preventDefault();
                    window.location.assign(newLink.href);
                }
                break;
            }
            case "enter": {
                const editor = document.querySelector(".catalog-editor__form");
                if (editor instanceof HTMLFormElement) {
                    event.preventDefault();
                    editor.requestSubmit();
                }
                break;
            }
            case "m": {
                const stateForm = document.querySelector(".catalog-state-form");
                if (stateForm instanceof HTMLFormElement) {
                    event.preventDefault();
                    stateForm.requestSubmit();
                }
                break;
            }
            case "f": {
                const search = document.getElementById("catalog-search");
                if (search instanceof HTMLInputElement) {
                    event.preventDefault();
                    search.focus();
                    search.select();
                }
                break;
            }
            case "p":
            case "s":
            case "g": {
                if (window.location.pathname !== "/catalog") {
                    break;
                }
                const view = event.key.toLowerCase() === "p" ? "products"
                    : event.key.toLowerCase() === "s" ? "services" : "categories";
                const url = new URL(window.location.href);
                url.searchParams.set("view", view);
                url.searchParams.delete("page");
                event.preventDefault();
                window.location.assign(url.pathname + url.search);
                break;
            }
            case "o": {
                const logoutForm = document.querySelector('.logout-form');
                if (logoutForm instanceof HTMLFormElement) {
                    event.preventDefault();
                    logoutForm.requestSubmit();
                }
                break;
            }
            default:
                break;
        }
    });
})();
