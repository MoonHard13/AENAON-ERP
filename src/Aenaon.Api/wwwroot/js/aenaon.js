// Ελάχιστη ενίσχυση προσιτότητας του στατικού Blazor UI· η πιστοποίηση γίνεται μόνο στον server.
(() => {
    "use strict";

    const storageKey = "aenaon-theme";
    const root = document.documentElement;

    function readTheme() {
        try {
            const value = window.localStorage.getItem(storageKey);
            return value === "dark" || value === "light" ? value : null;
        } catch {
            return null;
        }
    }

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
        const nextName = currentTheme() === "dark" ? "Φωτεινό" : "Σκούρο";
        document.querySelectorAll("[data-theme-label]").forEach(element => {
            element.textContent = nextName;
        });
    }

    function toggleTheme() {
        const nextTheme = currentTheme() === "dark" ? "light" : "dark";
        root.dataset.theme = nextTheme;
        saveTheme(nextTheme);
        updateThemeLabels();
    }

    const saved = readTheme();
    if (saved) {
        root.dataset.theme = saved;
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
            loginForm.addEventListener("submit", () => {
                const button = loginForm.querySelector('button[type="submit"]');
                if (button) {
                    button.disabled = true;
                    button.textContent = "Γίνεται σύνδεση…";
                }
            });
        }
    });

    document.addEventListener("keydown", event => {
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
