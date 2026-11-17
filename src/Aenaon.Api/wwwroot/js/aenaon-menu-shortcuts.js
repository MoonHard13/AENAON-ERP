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


/* Η μετακίνηση γίνεται αποκλειστικά μέσα στο τρέχον επίπεδο και αποθηκεύεται από το προστατευμένο POST. */
let menuDragging = null;
function clearMenuDrag() {
    document.querySelectorAll(".menu-builder .is-dragging, .menu-builder .is-drop-target")
        .forEach((element) => element.classList.remove("is-dragging", "is-drop-target"));
    menuDragging = null;
}
function reorderMenuTile(source, target) {
    const grid = source?.parentElement;
    if (!grid || target?.parentElement !== grid || source === target) {
        return;
    }
    const tiles = Array.from(grid.children).filter((tile) =>
        tile.matches("[data-menu-drag-tile]"));
    const position = tiles.indexOf(target) + 1;
    const form = source.querySelector("[data-menu-drag-form]");
    const field = form?.querySelector('input[name="targetPosition"]');
    if (!field || position < 1 || tiles.indexOf(source) === position - 1) {
        return;
    }
    field.value = String(position);
    form.requestSubmit();
}
document.addEventListener("dragstart", (event) => {
    const handle = event.target.closest?.(".menu-builder__drag-handle");
    const tile = handle?.closest("[data-menu-drag-tile]");
    const grid = tile?.parentElement;
    if (!tile || !grid?.matches("[data-menu-sortable]") || !event.dataTransfer) {
        return;
    }
    menuDragging = { tile, grid };
    event.dataTransfer.effectAllowed = "move";
    event.dataTransfer.setData("text/plain", "menu-reorder");
    tile.classList.add("is-dragging");
});
document.addEventListener("dragover", (event) => {
    if (!menuDragging) {
        return;
    }
    const tile = event.target.closest?.("[data-menu-drag-tile]");
    if (!tile || tile.parentElement !== menuDragging.grid || tile === menuDragging.tile) {
        return;
    }
    event.preventDefault();
    event.dataTransfer.dropEffect = "move";
    menuDragging.grid.querySelectorAll(".is-drop-target")
        .forEach((candidate) => candidate.classList.remove("is-drop-target"));
    tile.classList.add("is-drop-target");
});
document.addEventListener("drop", (event) => {
    if (!menuDragging) {
        return;
    }
    const tile = event.target.closest?.("[data-menu-drag-tile]");
    const source = menuDragging.tile;
    if (tile?.parentElement === menuDragging.grid && tile !== source) {
        event.preventDefault();
        reorderMenuTile(source, tile);
    }
    clearMenuDrag();
});
document.addEventListener("dragend", clearMenuDrag);
document.addEventListener("keydown", (event) => {
    const handle = event.target.closest?.(".menu-builder__drag-handle");
    if (!handle || (event.key !== "ArrowUp" && event.key !== "ArrowDown")) {
        return;
    }
    const tile = handle.closest("[data-menu-drag-tile]");
    const grid = tile?.parentElement;
    if (!grid?.matches("[data-menu-sortable]")) {
        return;
    }
    const tiles = Array.from(grid.children).filter((element) =>
        element.matches("[data-menu-drag-tile]"));
    const current = tiles.indexOf(tile);
    const target = tiles[current + (event.key === "ArrowUp" ? -1 : 1)];
    if (target) {
        event.preventDefault();
        reorderMenuTile(tile, target);
    }
});
