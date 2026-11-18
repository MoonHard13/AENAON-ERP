-- AENAON ERP / Step 9.6A.1 / Migration #13
-- ΜΟΝΟ ΑΝΑΓΝΩΣΗ: εκτελέστε στο SSMS με επιλεγμένη βάση AenaonERP_Dev.
-- Δεν πραγματοποιεί DDL, INSERT, UPDATE, DELETE ή εφαρμογή migration.
SET NOCOUNT ON;

IF DB_NAME() <> N'AenaonERP_Dev'
    THROW 51013, N'Λάθος βάση: η προεπισκόπηση επιτρέπεται μόνο στην AenaonERP_Dev.', 1;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 51014, N'Δεν βρέθηκε ιστορικό EF migrations.', 1;

IF (SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory) <> 12
    THROW 51015, N'Αναμένονται ακριβώς 12 εφαρμοσμένες migrations πριν την #13.', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20261117130000_DefaultStoreMenuCatalogSync'
)
    THROW 51016, N'Δεν έχει εφαρμοστεί η προαπαιτούμενη migration #12.', 1;

IF EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20261117170000_MenuBuilderHierarchyAndRootItems'
)
    THROW 51017, N'Η migration #13 είναι ήδη καταγεγραμμένη.', 1;

SELECT DB_NAME() AS DatabaseName, compatibility_level AS CompatibilityLevel
FROM sys.databases
WHERE database_id = DB_ID();

SELECT MigrationId, ProductVersion
FROM dbo.__EFMigrationsHistory
ORDER BY MigrationId;

SELECT
    (SELECT COUNT_BIG(*) FROM dbo.RestaurantMenus) AS Menus,
    (SELECT COUNT_BIG(*) FROM dbo.MenuDrafts) AS Drafts,
    (SELECT COUNT_BIG(*) FROM dbo.MenuDraftCategories) AS DraftCategories,
    (SELECT COUNT_BIG(*) FROM dbo.MenuDraftItems) AS DraftItems,
    (SELECT COUNT_BIG(*) FROM dbo.MenuDraftCategories
     WHERE CatalogCategoryId IS NOT NULL) AS CatalogLinkedCategories;

-- Προεπισκόπηση των κατηγοριών που η #13 θα συνδέσει με γονέα.
;WITH ParentCandidates AS
(
    SELECT child.TenantId, child.MenuId,
           child.Id AS ChildMenuCategoryId,
           child.Name AS ChildName,
           source.ParentCategoryId AS SourceParentCategoryId,
           parent.Id AS ParentMenuCategoryId,
           parent.Name AS ParentMenuCategoryName
    FROM dbo.MenuDraftCategories AS child
    INNER JOIN dbo.CatalogCategories AS source
        ON source.TenantId = child.TenantId
       AND source.Id = child.CatalogCategoryId
    LEFT JOIN dbo.MenuDraftCategories AS parent
        ON parent.TenantId = child.TenantId
       AND parent.MenuId = child.MenuId
       AND parent.CatalogCategoryId = source.ParentCategoryId
    WHERE source.ParentCategoryId IS NOT NULL
)
SELECT COUNT_BIG(*) AS ERPChildren,
       COALESCE(SUM(CASE WHEN ParentMenuCategoryId IS NOT NULL
                              AND ParentMenuCategoryId <> ChildMenuCategoryId
                         THEN 1 ELSE 0 END), 0) AS WillNest,
       COALESCE(SUM(CASE WHEN ParentMenuCategoryId IS NULL
                         THEN 1 ELSE 0 END), 0) AS ParentNotInMenu,
       COALESCE(SUM(CASE WHEN ParentMenuCategoryId = ChildMenuCategoryId
                         THEN 1 ELSE 0 END), 0) AS SelfReference
FROM ParentCandidates;

-- Ειδικότερα: παιδιά χωρίς συνδεδεμένο γονέα μένουν στη ρίζα.
;WITH UnresolvedParents AS
(
    SELECT child.TenantId, child.MenuId, child.Id AS ChildMenuCategoryId,
           child.Name AS ChildName,
           source.ParentCategoryId AS ExpectedERPParentId
    FROM dbo.MenuDraftCategories AS child
    INNER JOIN dbo.CatalogCategories AS source
        ON source.TenantId = child.TenantId
       AND source.Id = child.CatalogCategoryId
    LEFT JOIN dbo.MenuDraftCategories AS parent
        ON parent.TenantId = child.TenantId
       AND parent.MenuId = child.MenuId
       AND parent.CatalogCategoryId = source.ParentCategoryId
    WHERE source.ParentCategoryId IS NOT NULL AND parent.Id IS NULL
)
SELECT TOP (200) *
FROM UnresolvedParents
ORDER BY TenantId, MenuId, ChildName;

-- Προεπισκόπηση ιεραρχικών αλλαγών (χωρίς να γίνει UPDATE).
SELECT TOP (200)
       child.TenantId, child.MenuId,
       child.Id AS ChildMenuCategoryId,
       child.Name AS ChildName,
       child.Position AS PreviousPosition,
       parent.Id AS ProposedParentMenuCategoryId,
       parent.Name AS ProposedParentName
FROM dbo.MenuDraftCategories AS child
INNER JOIN dbo.CatalogCategories AS source
    ON source.TenantId = child.TenantId
   AND source.Id = child.CatalogCategoryId
INNER JOIN dbo.MenuDraftCategories AS parent
    ON parent.TenantId = child.TenantId
   AND parent.MenuId = child.MenuId
   AND parent.CatalogCategoryId = source.ParentCategoryId
WHERE parent.Id <> child.Id
ORDER BY child.TenantId, child.MenuId, child.Position;

-- Μη κανονικοποιημένες θέσεις θα αναριθμηθούν ανά επίπεδο.
SELECT TOP (200) TenantId, MenuId, Position,
       COUNT_BIG(*) AS DuplicatePositionCount
FROM dbo.MenuDraftCategories
GROUP BY TenantId, MenuId, Position
HAVING COUNT_BIG(*) > 1
ORDER BY DuplicatePositionCount DESC, TenantId, MenuId;

-- Το όριο ανά μενού πρέπει να χωρά στο εύρος θέσεων 1..10000.
SELECT TenantId, MenuId, COUNT_BIG(*) AS CategoryCount
FROM dbo.MenuDraftCategories
GROUP BY TenantId, MenuId
HAVING COUNT_BIG(*) > 10000;

-- Τυχόν ήδη ασυνεπή δεδομένα (τα υφιστάμενα FKs κανονικά τα αποκλείουν).
SELECT COUNT_BIG(*) AS OrphanDraftItems
FROM dbo.MenuDraftItems AS item
LEFT JOIN dbo.MenuDraftCategories AS category
    ON category.TenantId = item.TenantId
   AND category.MenuId = item.MenuId
   AND category.Id = item.MenuCategoryId
WHERE category.Id IS NULL;

SELECT COUNT_BIG(*) AS OrphanMenuDrafts
FROM dbo.MenuDraftCategories AS category
LEFT JOIN dbo.MenuDrafts AS draft
    ON draft.TenantId = category.TenantId
   AND draft.MenuId = category.MenuId
WHERE draft.MenuId IS NULL;

-- Οι νέοι κανόνες απαιτούν τα ακόλουθα ενεργά constraints/indexes πριν την αλλαγή.
SELECT OBJECT_NAME(i.object_id) AS TableName,
       i.name AS IndexName, i.is_unique AS IsUnique,
       i.filter_definition AS FilterDefinition
FROM sys.indexes AS i
WHERE i.object_id IN (
    OBJECT_ID(N'dbo.MenuDraftCategories'),
    OBJECT_ID(N'dbo.MenuDraftItems')
)
ORDER BY TableName, i.name;

-- Τέλος προεπισκόπησης. Καμία μεταβολή δεδομένων.
