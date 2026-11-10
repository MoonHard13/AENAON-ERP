// Το θέμα εφαρμόζεται πριν από το CSS για αποφυγή στιγμιαίας εμφάνισης λανθασμένων χρωμάτων.
(() => {
    "use strict";

    try {
        const preference = window.localStorage.getItem("aenaon-theme");
        if (preference === "light" || preference === "dark") {
            document.documentElement.dataset.theme = preference;
        }
    } catch {
        // Η προτίμηση του λειτουργικού συστήματος συνεχίζει να λειτουργεί χωρίς localStorage.
    }
})();
