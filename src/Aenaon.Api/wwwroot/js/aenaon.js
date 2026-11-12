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

    document.addEventListener("DOMContentLoaded", () => {
        updateThemeLabels();

        document.querySelectorAll("[data-theme-toggle]").forEach(button => {
            button.addEventListener("click", toggleTheme);
        });

        document.querySelectorAll("[data-password-toggle]").forEach(button => {
            button.addEventListener("click", () => {
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
        });

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
            case "c":
                event.preventDefault();
                window.location.assign("/catalog");
                break;
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
