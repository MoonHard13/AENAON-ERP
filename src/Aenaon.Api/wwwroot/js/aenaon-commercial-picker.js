/* Ενιαίος εμπορικός επιλογέας με ασφαλή native fallback και πλήρη χειρισμό πληκτρολογίου. */
(() => {
    "use strict";

    const MARGIN = 12;
    const GAP = 7;

    class CatalogCommercialPicker {
        constructor(root) {
            this.root = root;
            this.control = root.querySelector("[data-commercial-control]");
            this.search = root.querySelector("[data-commercial-search]");
            this.toggle = root.querySelector("[data-commercial-toggle]");
            this.popup = root.querySelector("[data-commercial-popup]");
            this.list = root.querySelector("[data-commercial-options]");
            this.status = root.querySelector("[data-commercial-status]");
            this.error = root.querySelector("[data-commercial-error]");
            this.native = root.querySelector("[data-commercial-native]");
            this.value = root.querySelector("[data-commercial-value]");
            this.form = root.closest("form");

            if (!this.control || !this.search || !this.toggle || !this.popup ||
                !this.list || !this.status || !this.error || !this.native ||
                !this.value || !this.form) {
                return;
            }

            this.options = [...this.native.options]
                .filter(option => option.value !== "" || root.dataset.required === "false")
                .map(option => ({
                    value: option.value,
                    label: option.textContent?.trim() || ""
                }));
            this.selectedValue = this.native.value;
            this.selectedLabel = this.options.find(item =>
                item.value === this.selectedValue)?.label || "";
            this.items = [];
            this.activeIndex = -1;
            this.isOpen = false;

            if (!this.options.length || (this.selectedValue &&
                !this.options.some(item => item.value === this.selectedValue))) {
                return;
            }

            this.installHandlers();

            // Ακριβώς ένα ενεργό πεδίο ανά name: το native παραμένει fallback χωρίς JS.
            this.value.value = this.selectedValue;
            this.value.disabled = false;
            this.native.disabled = true;
            this.search.value = this.selectedLabel;
            this.root.classList.add("catalog-category-picker--ready");
            this.root.dataset.commercialInitialized = "true";
        }

        installHandlers() {
            this.search.addEventListener("focus", () => this.open());
            this.search.addEventListener("click", () => this.open());
            this.search.addEventListener("input", () => {
                // Ελεύθερο κείμενο δεν αποτελεί έγκυρο enum ούτε αποστέλλεται στον server.
                this.value.value = "";
                this.hideError();
                this.open();
                this.render();
            });
            this.search.addEventListener("keydown", event => {
                switch (event.key) {
                    case "ArrowDown":
                    case "ArrowUp":
                        event.preventDefault();
                        this.open();
                        this.moveActive(event.key === "ArrowDown" ? 1 : -1);
                        break;
                    case "Home":
                    case "End":
                        if (this.isOpen && this.items.length) {
                            event.preventDefault();
                            this.activeIndex = event.key === "Home" ? 0 : this.items.length - 1;
                            this.updateActive();
                        }
                        break;
                    case "Enter":
                        event.preventDefault();
                        if (!this.isOpen) {
                            this.open();
                        } else if (this.activeIndex >= 0) {
                            this.choose(this.items[this.activeIndex]);
                        }
                        break;
                    case "Escape":
                        if (this.isOpen) {
                            event.preventDefault();
                            this.restoreSelected();
                            this.hideError();
                            this.close();
                        }
                        break;
                    case "Tab":
                        // Το Tab μεταφέρει φυσιολογικά την εστίαση στο επόμενο πεδίο.
                        this.close();
                        break;
                }
            });

            this.toggle.addEventListener("click", () => {
                if (this.isOpen) {
                    this.close();
                } else {
                    this.open();
                    this.search.focus({ preventScroll: true });
                }
            });

            // Διατηρούμε την εστίαση μέχρι να ολοκληρωθεί το click μιας επιλογής.
            this.list.addEventListener("mousedown", event => {
                if (event.target instanceof Element &&
                    event.target.closest("[data-commercial-index]")) {
                    event.preventDefault();
                }
            });
            this.list.addEventListener("click", event => {
                if (!(event.target instanceof Element)) {
                    return;
                }
                const option = event.target.closest("[data-commercial-index]");
                const index = Number(option?.getAttribute("data-commercial-index"));
                if (option && Number.isInteger(index) &&
                    index >= 0 && index < this.items.length) {
                    this.choose(this.items[index]);
                }
            });

            document.addEventListener("pointerdown", event => {
                if (!this.root.contains(event.target)) {
                    this.close();
                }
            });
            this.root.addEventListener("focusout", () => {
                window.queueMicrotask(() => {
                    if (!this.root.contains(document.activeElement)) {
                        this.close();
                    }
                });
            });

            this.form.addEventListener("submit", event => {
                // Η δημιουργία νέας κατηγορίας αποθηκεύει ανεξάρτητα το πρόχειρο είδος.
                if (event.submitter?.matches("[data-category-quick-submit]")) {
                    return;
                }
                if (this.options.some(item => item.value === this.value.value)) {
                    this.hideError();
                    return;
                }
                event.preventDefault();
                event.stopImmediatePropagation();
                this.showError();
                this.search.focus({ preventScroll: true });
                this.open();
            }, true);

            const adjust = event => {
                if (this.isOpen && event.target !== this.list) {
                    this.positionPopup();
                }
            };
            window.addEventListener("resize", adjust, { passive: true });
            window.addEventListener("scroll", adjust, { passive: true, capture: true });
            window.visualViewport?.addEventListener("resize", adjust, { passive: true });
            window.visualViewport?.addEventListener("scroll", adjust, { passive: true });
        }

        open() {
            if (!this.isOpen) {
                this.isOpen = true;
                this.popup.hidden = false;
                this.search.setAttribute("aria-expanded", "true");
                this.render();
            }
            this.positionPopup();
        }

        close() {
            if (!this.isOpen) {
                return;
            }
            this.isOpen = false;
            this.popup.hidden = true;
            this.search.setAttribute("aria-expanded", "false");
            this.search.removeAttribute("aria-activedescendant");
            this.activeIndex = -1;
        }

        render() {
            const term = this.search.value.trim();
            const filtered = term === this.selectedLabel ? "" : term.toLocaleLowerCase("el");
            this.items = this.options.filter(item =>
                !filtered || item.label.toLocaleLowerCase("el").includes(filtered));
            const fragment = document.createDocumentFragment();

            this.items.forEach((item, index) => {
                const option = document.createElement("div");
                option.id = this.list.id + "-option-" + index;
                option.className = "catalog-category-picker__option";
                option.setAttribute("role", "option");
                option.setAttribute("aria-selected",
                    item.value === this.value.value ? "true" : "false");
                option.dataset.commercialIndex = String(index);

                const icon = document.createElement("span");
                icon.className = "catalog-category-picker__option-icon";
                icon.textContent = "▤";

                const label = document.createElement("span");
                label.className = "catalog-category-picker__option-text";
                const title = document.createElement("strong");
                title.textContent = item.label;
                label.appendChild(title);
                option.append(icon, label);

                if (item.value === this.value.value) {
                    const check = document.createElement("span");
                    check.className = "catalog-category-picker__selected-indicator";
                    check.setAttribute("aria-hidden", "true");
                    check.textContent = "✓";
                    option.appendChild(check);
                }
                fragment.appendChild(option);
            });

            this.list.replaceChildren(fragment);
            this.activeIndex = -1;
            this.search.removeAttribute("aria-activedescendant");
            this.status.textContent = !this.items.length
                ? "Δεν βρέθηκαν επιλογές."
                : this.items.length === 1
                    ? "1 διαθέσιμη επιλογή."
                    : this.items.length + " διαθέσιμες επιλογές.";
            this.positionPopup();
        }

        choose(item) {
            if (!item || !this.options.some(option => option.value === item.value)) {
                return;
            }
            this.selectedValue = item.value;
            this.selectedLabel = item.label;
            this.value.value = item.value;
            this.search.value = item.label;
            this.hideError();
            this.close();
            this.search.focus({ preventScroll: true });
        }

        restoreSelected() {
            this.value.value = this.selectedValue;
            this.search.value = this.selectedLabel;
        }

        moveActive(direction) {
            if (!this.items.length) {
                return;
            }
            this.activeIndex = this.activeIndex < 0
                ? direction > 0 ? 0 : this.items.length - 1
                : (this.activeIndex + direction + this.items.length) % this.items.length;
            this.updateActive();
        }

        updateActive() {
            [...this.list.children].forEach((option, index) => {
                const active = index === this.activeIndex;
                option.classList.toggle("catalog-category-picker__option--active", active);
                option.setAttribute("data-active", active ? "true" : "false");
                if (active) {
                    this.search.setAttribute("aria-activedescendant", option.id);
                    option.scrollIntoView({ block: "nearest", inline: "nearest" });
                }
            });
            if (this.activeIndex < 0) {
                this.search.removeAttribute("aria-activedescendant");
            }
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
            const below = Math.max(0, bottom - rect.bottom - GAP - MARGIN);
            const above = Math.max(0, rect.top - top - GAP - MARGIN);
            const openAbove = below < 245 && above > below;
            const available = openAbove ? above : below;
            const useViewportPanel = available < 135;
            const panelHeight = Math.min(360, useViewportPanel
                ? Math.max(0, height - 2 * MARGIN) : available);
            const panelWidth = Math.max(0, Math.min(620, rect.width,
                width - MARGIN * 2));
            const panelLeft = Math.max(left + MARGIN,
                Math.min(rect.left, right - MARGIN - panelWidth));

            this.popup.classList.toggle("catalog-category-picker__popup--compact",
                panelHeight < 135);
            const statusHeight = panelHeight < 135 ? 0 : Math.max(36, this.status.offsetHeight);
            this.list.style.maxHeight = Math.max(0,
                Math.min(300, panelHeight - statusHeight - 18)) + "px";
            this.popup.style.width = panelWidth + "px";
            this.popup.style.left = panelLeft + "px";
            this.popup.style.maxHeight = panelHeight + "px";

            const actualHeight = Math.min(this.popup.offsetHeight, panelHeight);
            const idealTop = useViewportPanel ? top + MARGIN
                : openAbove ? rect.top - GAP - actualHeight : rect.bottom + GAP;
            this.popup.style.top = Math.max(top + MARGIN,
                Math.min(idealTop, bottom - MARGIN - actualHeight)) + "px";
            this.popup.dataset.placement = useViewportPanel
                ? "viewport" : openAbove ? "top" : "bottom";
        }

        showError() {
            this.error.hidden = false;
            this.search.setAttribute("aria-invalid", "true");
        }

        hideError() {
            this.error.hidden = true;
            this.search.removeAttribute("aria-invalid");
        }
    }

    function initialize() {
        document.querySelectorAll("[data-commercial-picker]").forEach(root => {
            if (root.dataset.commercialInitialized === "true") {
                return;
            }
            new CatalogCommercialPicker(root);
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
    if (window.Blazor && typeof window.Blazor.addEventListener === "function") {
        window.Blazor.addEventListener("enhancedload", initialize);
    }
})();
