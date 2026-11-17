/* Η συντόμευση εφαρμόζεται μόνο όταν η εστίαση βρίσκεται σε ενεργή φόρμα του Menu Builder. */
document.addEventListener("keydown", (event) => {
    if ((!event.ctrlKey && !event.metaKey) || event.altKey || event.shiftKey ||
        event.key.toLowerCase() !== "s") {
        return;
    }

    const active = document.activeElement;
    const form = active?.closest?.(".menu-builder form[data-menu-save='true']");
    if (!form || !form.checkValidity()) {
        return;
    }

    event.preventDefault();
    form.requestSubmit();
});
