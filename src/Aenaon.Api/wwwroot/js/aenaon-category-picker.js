/* Προσβάσιμο combobox με προσαρμογή viewport, ασφαλή αναζήτηση και native fallback. */
(() => {
    "use strict";

    const SEARCH_DELAY_MS = 220;
    const MAX_RESULTS = 30;
    const VIEWPORT_MARGIN = 12;
    const POPUP_GAP = 7;

    class CatalogCategoryPicker {
        constructor(root) {
            this.root = root;
            this.control = root.querySelector("[data-picker-control]");
            this.input = root.querySelector("[data-picker-search]");
            this.toggle = root.querySelector("[data-picker-toggle]");
            this.popup = root.querySelector("[data-picker-popup]");
            this.list = root.querySelector("[data-picker-options]");
            this.status = root.querySelector("[data-picker-status]");
            this.error = root.querySelector("[data-picker-error]");
            this.native = root.querySelector("[data-picker-native]");
            this.hidden = root.querySelector("[data-picker-value]");
            this.form = root.closest("form");
            this.createButtons = [...root.querySelectorAll("[data-picker-create]")];
            this.createPopup = this.popup?.querySelector("[data-picker-create]");
            this.tenantId = root.dataset.tenantId;
            this.excludedId = root.dataset.selfId || "";
            this.required = root.dataset.required === "true";

            this.items = [];
            this.activeIndex = -1;
            this.request = null;
            this.searchDelay = null;
            this.sequence = 0;
            this.isOpen = false;
            this.isSubmittingQuickCreate = false;

            if (!this.control || !this.input || !this.toggle || !this.popup ||
                !this.list || !this.status || !this.error || !this.native ||
                !this.hidden || !this.form || !this.tenantId) {
                return;
            }

            this.selectedId = this.native.value;
            this.selectedLabel = this.selectedId
                ? this.native.selectedOptions[0]?.textContent?.trim() || ""
                : "";

            // Το native select παρέχει τις αρχικές επιλογές αν το δίκτυο καθυστερεί.
            this.initialItems = [...this.native.options]
                .filter(option => option.value)
                .map(option => {
                    const label = option.textContent?.trim() || "";
                    const separator = label.indexOf(" — ");
                    const path = separator < 0 ? label : label.slice(separator + 3);
                    return {
                        id: option.value,
                        code: separator < 0 ? "" : label.slice(0, separator),
                        name: path.split(" / ").at(-1) || path,
                        path
                    };
                });

            this.installHandlers();

            // Προοδευτική ενεργοποίηση μόνο αφού είναι διαθέσιμο όλο το control.
            this.hidden.value = this.selectedId;
            this.hidden.disabled = false;
            this.native.disabled = true;
            this.input.value = this.selectedLabel;
            this.root.classList.add("catalog-category-picker--ready");
        }

        installHandlers() {
            this.input.addEventListener("focus", () => this.open());
            this.input.addEventListener("input", () => {
                this.hidden.value = "";
                this.hideError();
                this.open(false);
                // Ένα μόνο debounce ανά αλλαγή, όχι open() + δεύτερο scheduleSearch().
                this.scheduleSearch(this.input.value.trim());
            });

            this.input.addEventListener("keydown", event => {
                if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                    event.preventDefault();
                    this.open();
                    this.moveActive(event.key === "ArrowDown" ? 1 : -1);
                    return;
                }

                if (event.key === "Enter") {
                    // Το Enter επιλέγει κατηγορία και δεν υποβάλλει τη φόρμα.
                    event.preventDefault();
                    if (this.isOpen && this.activeIndex >= 0) {
                        this.choose(this.items[this.activeIndex]);
                    } else if (!this.isOpen) {
                        this.open();
                    }
                    return;
                }

                if (event.key === "Escape" && this.isOpen) {
                    event.preventDefault();
                    this.restoreSelected();
                    this.hideError();
                    this.close();
                    return;
                }

                if (event.key === "Tab" && this.isOpen) {
                    if (!event.shiftKey && this.createPopup) {
                        event.preventDefault();
                        this.createPopup.focus({ preventScroll: true });
                    } else {
                        this.close();
                    }
                }
            });

            this.toggle.addEventListener("click", () => {
                if (this.isOpen) {
                    this.close();
                } else {
                    this.open();
                    this.input.focus({ preventScroll: true });
                }
            });

            // Η απώλεια focus από το input δεν πρέπει να κλείνει το popup
            // προτού παραδοθεί το click σε επιλογή ή στο κουμπί νέας κατηγορίας.
            this.popup.addEventListener("mousedown", event => {
                if (event.target instanceof Element &&
                    event.target.closest('[role="option"], [data-picker-create]')) {
                    event.preventDefault();
                }
            });

            this.list.addEventListener("click", event => {
                if (!(event.target instanceof Element)) {
                    return;
                }
                const option = event.target.closest("[data-option-index]");
                const index = Number(option?.getAttribute("data-option-index"));
                if (option && Number.isInteger(index) &&
                    index >= 0 && index < this.items.length) {
                    this.choose(this.items[index]);
                }
            });

            for (const button of this.createButtons) {
                button.addEventListener("click", event => {
                    event.preventDefault();
                    this.requestQuickCreate();
                });
            }

            document.addEventListener("pointerdown", event => {
                if (!this.root.contains(event.target)) {
                    this.close();
                }
            });

            this.root.addEventListener("focusout", () => {
                window.queueMicrotask(() => {
                    if (!this.root.contains(document.activeElement) &&
                        !this.isSubmittingQuickCreate) {
                        this.close();
                    }
                });
            });

            this.form.addEventListener("submit", event => {
                if (event.submitter?.matches("[data-category-quick-submit]")) {
                    return;
                }

                const emptyAllowed = !this.required &&
                    (!this.input.value.trim() ||
                     (this.selectedId === "" &&
                      this.input.value === this.selectedLabel));
                if (this.hidden.value || emptyAllowed) {
                    this.hideError();
                    return;
                }

                event.preventDefault();
                event.stopImmediatePropagation();
                this.showError();
                this.input.focus({ preventScroll: true });
                this.open();
            }, true);

            // Έλεγχος θέσης σε window scroll, εσωτερικό scroll φόρμας και zoom.
            const adjust = event => {
                if (this.isOpen && event.target !== this.list) {
                    this.positionPopup();
                }
            };
            window.addEventListener("resize", adjust, { passive: true });
            window.addEventListener("scroll", adjust, { capture: true, passive: true });
            window.visualViewport?.addEventListener("resize", adjust, { passive: true });
            window.visualViewport?.addEventListener("scroll", adjust, { passive: true });
        }

        requestQuickCreate() {
            if (!this.form || this.isSubmittingQuickCreate) {
                return;
            }

            const submit = this.form.querySelector("[data-category-quick-submit]");
            if (!(submit instanceof HTMLButtonElement)) {
                this.status.textContent = "Δεν είναι διαθέσιμη η δημιουργία κατηγορίας.";
                this.open(false);
                return;
            }

            this.isSubmittingQuickCreate = true;
            this.close();

            // Χρησιμοποιούμε το πραγματικό HTML submitter με formaction/formnovalidate,
            // ώστε ο server να κρατήσει τα πρόχειρα στοιχεία και το εγκεκριμένο store.
            try {
                this.form.requestSubmit(submit);
            } catch {
                this.isSubmittingQuickCreate = false;
                this.status.textContent = "Η μετάβαση δεν ολοκληρώθηκε. Δοκιμάστε ξανά.";
                this.open(false);
            }
        }

        open(search = true) {
            if (!this.isOpen) {
                this.popup.hidden = false;
                this.input.setAttribute("aria-expanded", "true");
                this.isOpen = true;
                this.render(this.initialItems);
            }

            this.positionPopup();
            if (search && !this.request && this.searchDelay === null) {
                const isCommitted = this.input.value === this.selectedLabel;
                this.scheduleSearch(isCommitted ? "" : this.input.value.trim());
            }
        }

        close() {
            if (!this.isOpen) {
                return;
            }
            this.popup.hidden = true;
            this.input.setAttribute("aria-expanded", "false");
            this.input.removeAttribute("aria-activedescendant");
            this.root.removeAttribute("aria-busy");
            this.isOpen = false;
            this.activeIndex = -1;
            this.sequence++;
            this.request?.abort();
            this.request = null;
            window.clearTimeout(this.searchDelay);
            this.searchDelay = null;
        }

        restoreSelected() {
            this.input.value = this.selectedLabel;
            this.hidden.value = this.selectedId;
        }

        choose(item) {
            if (!item || (!item.id && !item.isRoot) ||
                (item.id && item.id === this.excludedId)) {
                return;
            }

            this.selectedId = item.isRoot ? "" : item.id;
            this.selectedLabel = item.isRoot ? "Καμία — κύρια κατηγορία"
                : item.code + " — " + (item.path || item.name);
            this.hidden.value = this.selectedId;
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
                element.classList.toggle("catalog-category-picker__option--active", active);
                element.setAttribute("data-active", active ? "true" : "false");
                if (active) {
                    this.input.setAttribute("aria-activedescendant", element.id);
                    element.scrollIntoView({ block: "nearest", inline: "nearest" });
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
            this.root.setAttribute("aria-busy", "true");
            this.searchDelay = window.setTimeout(() => {
                this.searchDelay = null;
                void this.search(term, current);
            }, SEARCH_DELAY_MS);
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
                    throw new Error("Category lookup failed");
                }
                const items = await response.json();
                if (sequence !== this.sequence || !this.isOpen) {
                    return;
                }

                this.render(items);
                this.status.textContent = !this.items.length
                    ? "Δεν βρέθηκαν κατηγορίες."
                    : this.items.length === MAX_RESULTS
                        ? "Εμφανίζονται έως 30 κατηγορίες. Περιορίστε την αναζήτηση."
                        : this.items.length === 1
                            ? "Βρέθηκε 1 κατηγορία."
                            : `Βρέθηκαν ${this.items.length} κατηγορίες.`;
                this.positionPopup();
            } catch {
                if (!controller.signal.aborted && sequence === this.sequence) {
                    this.status.textContent = "Η αναζήτηση απέτυχε. Πληκτρολογήστε ξανά.";
                    this.positionPopup();
                }
            } finally {
                if (sequence === this.sequence) {
                    this.request = null;
                    this.root.removeAttribute("aria-busy");
                }
            }
        }

        render(items) {
            const candidates = Array.isArray(items) ? items : [];
            const selectable = !this.required
                ? [{ id: "", code: "", name: "Καμία — κύρια κατηγορία",
                     path: "", isRoot: true }, ...candidates]
                : candidates;
            const term = this.input.value.trim();
            const committed = term === this.selectedLabel;
            const searchTerm = committed ? "" : term.toLocaleLowerCase("el");
            this.items = selectable
                .filter(item => (item?.id || item?.isRoot) &&
                    item.id !== this.excludedId)
                .filter(item => !searchTerm || item.isRoot ||
                    item.code?.toLocaleLowerCase("el").startsWith(searchTerm) ||
                    item.name?.toLocaleLowerCase("el").startsWith(searchTerm))
                .slice(0, MAX_RESULTS);

            const fragment = document.createDocumentFragment();
            this.items.forEach((item, index) => {
                const option = document.createElement("div");
                option.id = this.list.id + "-option-" + index;
                option.className = "catalog-category-picker__option";
                option.setAttribute("role", "option");
                option.setAttribute("aria-selected",
                    item.id === this.selectedId ? "true" : "false");
                option.dataset.optionIndex = String(index);

                const icon = document.createElement("span");
                icon.className = "catalog-category-picker__option-icon";
                icon.textContent = item.isRoot ? "—" : item.parentId ? "↳" : "▤";

                const texts = document.createElement("span");
                texts.className = "catalog-category-picker__option-text";
                const title = document.createElement("strong");
                title.textContent = item.name || item.path || item.code || "";
                const path = document.createElement("small");
                const pathText = item.path && item.path !== item.name
                    ? item.path + " · " : "";
                path.textContent = item.isRoot
                    ? "Χωρίς γονική κατηγορία"
                    : pathText + "Κωδικός " + (item.code || "");
                texts.append(title, path);
                option.append(icon, texts);

                if ((item.isRoot && this.selectedId === "") ||
                    (item.id && item.id === this.selectedId)) {
                    const check = document.createElement("span");
                    check.className = "catalog-category-picker__selected-indicator";
                    check.textContent = "✓";
                    check.setAttribute("aria-hidden", "true");
                    option.appendChild(check);
                }
                fragment.appendChild(option);
            });

            this.list.replaceChildren(fragment);
            this.activeIndex = -1;
            this.input.removeAttribute("aria-activedescendant");
        }

        positionPopup() {
            if (!this.isOpen) {
                return;
            }

            const rect = this.control.getBoundingClientRect();
            const viewport = window.visualViewport;
            const top = viewport?.offsetTop || 0;
            const left = viewport?.offsetLeft || 0;
            const width = viewport?.width || window.innerWidth;
            const height = viewport?.height || window.innerHeight;
            const bottom = top + height;
            const right = left + width;
            const availableBelow = Math.max(0, bottom - rect.bottom - POPUP_GAP -
                VIEWPORT_MARGIN);
            const availableAbove = Math.max(0, rect.top - top - POPUP_GAP -
                VIEWPORT_MARGIN);
            const openAbove = availableBelow < 270 && availableAbove > availableBelow;

            // Περιορίζουμε πραγματικά το popup στο ορατό viewport, ακόμη και με zoom.
            const available = openAbove ? availableAbove : availableBelow;
            const panelHeight = Math.max(105, Math.min(370, available || height - 24));
            const safeWidth = Math.max(0, width - VIEWPORT_MARGIN * 2);
            const panelWidth = Math.min(620, rect.width, safeWidth);
            const panelLeft = Math.max(left + VIEWPORT_MARGIN,
                Math.min(rect.left, right - VIEWPORT_MARGIN - panelWidth));
            const footerHeight = this.createPopup?.offsetHeight || 0;
            const statusHeight = Math.max(this.status.offsetHeight, 36);
            const listHeight = Math.max(46, panelHeight - footerHeight - statusHeight - 23);

            this.list.style.maxHeight = Math.min(300, listHeight) + "px";
            this.popup.style.width = panelWidth + "px";
            this.popup.style.left = panelLeft + "px";
            this.popup.style.maxHeight = panelHeight + "px";

            const actualHeight = Math.min(this.popup.offsetHeight, panelHeight);
            const idealTop = openAbove
                ? rect.top - POPUP_GAP - actualHeight
                : rect.bottom + POPUP_GAP;
            const panelTop = Math.max(top + VIEWPORT_MARGIN,
                Math.min(idealTop, bottom - VIEWPORT_MARGIN - actualHeight));

            this.popup.style.top = panelTop + "px";
            this.popup.dataset.placement = openAbove ? "top" : "bottom";
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
            const picker = new CatalogCategoryPicker(root);
            if (picker.input && picker.hidden && picker.form) {
                root.dataset.pickerInitialized = "true";
            }
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }

    // Αν ενεργοποιηθεί enhanced navigation, αρχικοποιούμε ξανά τα νέα SSR elements.
    if (window.Blazor && typeof window.Blazor.addEventListener === "function") {
        window.Blazor.addEventListener("enhancedload", initialize);
    }
})();
