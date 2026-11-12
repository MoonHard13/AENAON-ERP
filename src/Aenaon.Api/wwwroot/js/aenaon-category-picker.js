/* Προσβάσιμο category combobox: ο native select παραμένει fallback χωρίς JavaScript. */
(() => {
    "use strict";

    const MAX_RESULTS = 30;
    const SEARCH_DELAY_MS = 220;

    class CatalogCategoryPicker {
        constructor(root) {
            this.root = root;
            this.input = root.querySelector("[data-picker-search]");
            this.toggle = root.querySelector("[data-picker-toggle]");
            this.popup = root.querySelector("[data-picker-popup]");
            this.list = root.querySelector("[data-picker-options]");
            this.status = root.querySelector("[data-picker-status]");
            this.error = root.querySelector("[data-picker-error]");
            this.native = root.querySelector("[data-picker-native]");
            this.hidden = root.querySelector("[data-picker-value]");
            this.create = root.querySelector("[data-picker-create]");
            this.form = root.closest("form");
            this.tenantId = root.dataset.tenantId;
            this.excludedId = root.dataset.selfId || "";
            this.required = root.dataset.required === "true";
            this.items = [];
            this.activeIndex = -1;
            this.searchDelay = null;
            this.request = null;
            this.sequence = 0;
            this.isOpen = false;

            if (!this.input || !this.list || !this.native || !this.hidden ||
                !this.form || !this.tenantId) {
                return;
            }

            const nativeSelected = this.native.selectedOptions[0];
            this.selectedId = this.native.value;
            this.selectedLabel = this.selectedId ? nativeSelected?.textContent?.trim() || "" : "";
            this.initialItems = [...this.native.options].filter(option => option.value)
                .map(option => ({
                    id: option.value,
                    code: option.textContent?.split(" — ")[0] || "",
                    name: option.textContent?.trim() || "",
                    path: option.textContent?.split(" — ").slice(1).join(" — ") || ""
                }));

            // Η εναλλαγή γίνεται μόνο αφού ολοκληρωθεί πλήρως η αρχικοποίηση.
            this.native.disabled = true;
            this.hidden.disabled = false;
            this.hidden.value = this.selectedId;
            this.root.classList.add("catalog-category-picker--ready");
            this.input.value = this.selectedLabel;
            this.installHandlers();
        }

        installHandlers() {
            this.input.addEventListener("focus", () => this.open());
            this.input.addEventListener("input", () => {
                this.hidden.value = "";
                this.hideError();
                const search = this.input.value.trim();
                if (!search && !this.required) {
                    this.selectedId = "";
                    this.selectedLabel = "";
                }
                this.open();
                this.scheduleSearch(search);
            });

            this.input.addEventListener("keydown", event => {
                if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                    event.preventDefault();
                    if (!this.isOpen) {
                        this.open();
                    }
                    this.moveActive(event.key === "ArrowDown" ? 1 : -1);
                    return;
                }

                if (event.key === "Enter") {
                    // Το Enter δεν πρέπει ποτέ να υποβάλει έμμεσα την κύρια φόρμα.
                    event.preventDefault();
                    if (this.isOpen && this.activeIndex >= 0) {
                        this.choose(this.items[this.activeIndex]);
                    } else if (!this.isOpen) {
                        this.open();
                    }
                    return;
                }

                if (event.key === "Escape") {
                    if (this.isOpen) {
                        event.preventDefault();
                        this.restoreSelected();
                        this.close();
                    }
                    return;
                }

                if (event.key === "Tab") {
                    this.restoreSelected();
                    this.close();
                }
            });

            this.toggle?.addEventListener("click", () => {
                if (this.isOpen) {
                    this.close();
                } else {
                    this.open();
                    this.input.focus({ preventScroll: true });
                }
            });

            // Mousedown πριν από blur: διατηρούμε εστίαση στο input για ARIA active-descendant.
            this.list.addEventListener("pointerdown", event => {
                if (event.target instanceof Element && event.target.closest('[role="option"]')) {
                    event.preventDefault();
                }
            });
            this.list.addEventListener("click", event => {
                if (!(event.target instanceof Element)) {
                    return;
                }
                const element = event.target.closest("[data-option-index]");
                const index = Number(element?.getAttribute("data-option-index"));
                if (element && Number.isInteger(index) && index >= 0 && index < this.items.length) {
                    this.choose(this.items[index]);
                }
            });

            document.addEventListener("pointerdown", event => {
                if (!this.root.contains(event.target)) {
                    this.restoreSelected();
                    this.close();
                }
            });

            this.form.addEventListener("submit", event => {
                if (event.submitter?.matches("[data-category-quick-submit]")) {
                    return;
                }
                if (this.hidden.value ||
                    (!this.required && !this.input.value.trim())) {
                    this.hideError();
                    return;
                }

                event.preventDefault();
                event.stopImmediatePropagation();
                this.showError();
                this.input.focus({ preventScroll: true });
                this.open();
            }, true);

            this.create?.addEventListener("click", () => this.requestQuickCreate());
        }

        requestQuickCreate() {
            if (!this.form) {
                return;
            }

            let submit = this.form.querySelector("[data-category-quick-submit]");
            if (!(submit instanceof HTMLButtonElement)) {
                submit = document.createElement("button");
                submit.type = "submit";
                submit.hidden = true;
                submit.tabIndex = -1;
                submit.dataset.categoryQuickSubmit = "true";
                submit.formAction = "/ui/catalog/new-category";
                submit.formMethod = "post";
                submit.formNoValidate = true;
                this.form.appendChild(submit);
            }

            this.close();
            this.form.requestSubmit(submit);
        }

        open() {
            if (!this.isOpen) {
                this.popup.hidden = false;
                this.input.setAttribute("aria-expanded", "true");
                this.isOpen = true;
            }

            this.render(this.initialItems, false);
            // Επιλογή ήδη καταχωρισμένης κατηγορίας δεν πρέπει να χρησιμοποιείται ως όρος αναζήτησης.
            this.scheduleSearch(this.hidden.value ? "" : this.input.value.trim());
        }

        close() {
            this.popup.hidden = true;
            this.input.setAttribute("aria-expanded", "false");
            this.input.removeAttribute("aria-activedescendant");
            this.isOpen = false;
            this.activeIndex = -1;
            this.sequence++;
            this.request?.abort();
            this.request = null;
            window.clearTimeout(this.searchDelay);
        }

        restoreSelected() {
            this.input.value = this.selectedLabel;
            this.hidden.value = this.selectedId;
        }

        choose(item) {
            if (!item || !item.id || item.id === this.excludedId) {
                return;
            }

            this.selectedId = item.id;
            this.selectedLabel = item.code + " — " + (item.path || item.name);
            this.hidden.value = item.id;
            this.input.value = this.selectedLabel;
            this.hideError();
            this.close();
            this.input.focus({ preventScroll: true });
        }

        moveActive(direction) {
            if (!this.items.length) {
                return;
            }
            const length = this.items.length;
            this.activeIndex = this.activeIndex < 0
                ? direction > 0 ? 0 : length - 1
                : (this.activeIndex + direction + length) % length;
            this.updateActive();
        }

        updateActive() {
            [...this.list.children].forEach((element, index) => {
                const active = index === this.activeIndex;
                element.setAttribute("aria-selected", active ? "true" : "false");
                if (active) {
                    this.input.setAttribute("aria-activedescendant", element.id);
                    element.scrollIntoView({ block: "nearest" });
                }
            });
            if (this.activeIndex < 0) {
                this.input.removeAttribute("aria-activedescendant");
            }
        }

        scheduleSearch(term) {
            window.clearTimeout(this.searchDelay);
            this.sequence++;
            this.request?.abort();
            this.request = null;
            const current = this.sequence;
            this.status.textContent = "Αναζήτηση κατηγοριών…";
            this.status.hidden = false;
            this.root.setAttribute("aria-busy", "true");
            this.searchDelay = window.setTimeout(() => this.search(term, current),
                SEARCH_DELAY_MS);
        }

        async search(term, sequence) {
            const controller = new AbortController();
            this.request = controller;
            const params = new URLSearchParams({
                tenantId: this.tenantId,
                search: term
            });
            if (this.selectedId) {
                params.set("selectedId", this.selectedId);
            }
            try {
                const response = await window.fetch(
                    "/ui/catalog/categories?" + params.toString(), {
                        method: "GET",
                        credentials: "same-origin",
                        headers: { Accept: "application/json" },
                        signal: controller.signal
                    });
                if (!response.ok) {
                    throw new Error("Unable to retrieve categories");
                }
                const items = await response.json();
                if (sequence !== this.sequence || !this.isOpen) {
                    return;
                }

                this.render(items, true);
                this.status.textContent = !this.items.length
                    ? "Δεν βρέθηκαν κατηγορίες."
                    : this.items.length === MAX_RESULTS
                        ? "Εμφανίζονται έως 30 κατηγορίες. Πληκτρολογήστε για πιο συγκεκριμένη αναζήτηση."
                        : this.items.length === 1
                            ? "Βρέθηκε 1 κατηγορία."
                            : `Βρέθηκαν ${this.items.length} κατηγορίες.`;
            } catch (error) {
                if (controller.signal.aborted || sequence !== this.sequence) {
                    return;
                }
                this.status.textContent = "Η αναζήτηση απέτυχε. Δοκιμάστε ξανά.";
            } finally {
                if (sequence === this.sequence) {
                    this.root.removeAttribute("aria-busy");
                }
            }
        }

        render(items, fetched) {
            this.items = (Array.isArray(items) ? items : [])
                .filter(item => item?.id && item.id !== this.excludedId)
                .filter(item => !fetched || !this.input.value.trim() ||
                    !this.hidden.value || item.id === this.selectedId ||
                    item.code?.toLocaleLowerCase("el").startsWith(
                        this.input.value.trim().toLocaleLowerCase("el")) ||
                    item.name?.toLocaleLowerCase("el").startsWith(
                        this.input.value.trim().toLocaleLowerCase("el")))
                .slice(0, MAX_RESULTS);
            const options = document.createDocumentFragment();
            this.items.forEach((item, index) => {
                const option = document.createElement("div");
                option.id = "catalog-category-option-" + index;
                option.className = "catalog-category-picker__option";
                option.setAttribute("role", "option");
                option.setAttribute("aria-selected", "false");
                option.dataset.optionIndex = String(index);

                const icon = document.createElement("span");
                icon.className = "catalog-category-picker__option-icon";
                icon.textContent = item.parentId ? "↳" : "▤";

                const texts = document.createElement("span");
                texts.className = "catalog-category-picker__option-text";
                const title = document.createElement("strong");
                title.textContent = item.name || item.path || item.code || "";
                const path = document.createElement("small");
                path.textContent = (item.path || item.name || "") + " · Κωδικός " + (item.code || "");
                texts.append(title, path);
                option.append(icon, texts);
                if (item.id === this.selectedId) {
                    const check = document.createElement("span");
                    check.className = "catalog-category-picker__selected-indicator";
                    check.textContent = "✓";
                    check.setAttribute("aria-hidden", "true");
                    option.appendChild(check);
                }
                options.appendChild(option);
            });
            this.list.replaceChildren(options);
            this.activeIndex = -1;
            this.input.removeAttribute("aria-activedescendant");
        }

        showError() {
            this.error.hidden = false;
            this.input.setAttribute("aria-invalid", "true");
        }

        hideError() {
            this.error.hidden = true;
            this.input.removeAttribute("aria-invalid");
        }
    }

    function initialize() {
        document.querySelectorAll("[data-catalog-picker]").forEach(root => {
            if (root.dataset.pickerInitialized === "true") {
                return;
            }
            root.dataset.pickerInitialized = "true";
            new CatalogCategoryPicker(root);
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
