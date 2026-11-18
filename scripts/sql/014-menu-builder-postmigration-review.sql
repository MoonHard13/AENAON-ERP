-- AENAON ERP / Migration #13: έλεγχος δεδομένων ΜΕΤΑ την εφαρμογή.
-- Μόνο ανάγνωση, μόνο Development, χωρίς DDL ή μεταβολή δεδομένων.
SET NOCOUNT ON;

IF DB_NAME() <> N'AenaonERP_Dev'
    THROW 51020, N'Λάθος βάση: απαιτείται AenaonERP_Dev.', 1;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 51021, N'Δεν βρέθηκε πίνακας ιστορικού EF migrations.', 1;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20261117170000_MenuBuilderHierarchyAndRootItems'
)
    THROW 51022, N'Η migration #13 δεν έχει καταγραφεί ως εφαρμοσμένη.', 1;

IF COL_LENGTH(N'dbo.MenuDraftCategories', N'ParentCategoryId') IS NULL
   OR COL_LENGTH(N'dbo.MenuDraftItems', N'MenuCategoryId') IS NULL
    THROW 51023, N'Δεν βρέθηκαν οι απαιτούμενες στήλες.', 1;

SELECT DB_NAME() AS DatabaseName, compatibility_level AS CompatibilityLevel
FROM sys.databases
WHERE database_id = DB_ID();

SELECT COUNT_BIG(*) AS AppliedMigrations,
    SUM(CASE WHEN MigrationId = N'20261117170000_MenuBuilderHierarchyAndRootItems' THEN 1 ELSE 0 END)
    AS Migration13Recorded
FROM dbo.__EFMigrationsHistory;

-- Ιστορικό πριν από την #13 (με βάση την επιβεβαίωση 18/11/2026):
-- 3 μενού, 3 drafts, 27 κατηγορίες, 39 είδη, 19 συνδεδεμένες ERP κατηγορίες.
SELECT
    (SELECT COUNT_BIG(*) FROM dbo.RestaurantMenus) AS Menus,
    (SELECT COUNT_BIG(*) FROM dbo.MenuDrafts) AS Drafts,
    (SELECT COUNT_BIG(*) FROM dbo.MenuDraftCategories) AS DraftCategories,
    (SELECT COUNT_BIG(*) FROM dbo.MenuDraftItems) AS DraftItems,
    (SELECT COUNT_BIG(*) FROM dbo.MenuDraftCategories
        WHERE CatalogCategoryId IS NOT NULL) AS CatalogLinkedCategories,
    (SELECT COUNT_BIG(*) FROM dbo.MenuDraftCategories
        WHERE ParentCategoryId IS NOT NULL) AS NestedCategories,
    (SELECT COUNT_BIG(*) FROM dbo.MenuDraftItems
        WHERE MenuCategoryId IS NULL) AS RootItems;

-- Η προηγούμενη προεπισκόπηση βρήκε 6 συνδέσεις ERP παιδιού με γονέα.
SELECT child.TenantId, child.MenuId, child.Id AS ChildId,
       child.Name AS ChildName,
       parent.Id AS ParentId, parent.Name AS ParentName,
       child.Position AS ChildPosition
FROM dbo.MenuDraftCategories AS child
INNER JOIN dbo.MenuDraftCategories AS parent
    ON parent.TenantId = child.TenantId
   AND parent.MenuId = child.MenuId
   AND parent.Id = child.ParentCategoryId
ORDER BY child.MenuId, parent.Name, child.Position, child.Id;

-- Αναμενόμενο αποτέλεσμα 0: καμία σύνδεση parent εκτός Tenant ή Menu.
SELECT COUNT_BIG(*) AS InvalidParentLinks
FROM dbo.MenuDraftCategories AS child
LEFT JOIN dbo.MenuDraftCategories AS parent
    ON parent.TenantId = child.TenantId
   AND parent.MenuId = child.MenuId
   AND parent.Id = child.ParentCategoryId
WHERE child.ParentCategoryId IS NOT NULL
  AND (parent.Id IS NULL OR child.Id = child.ParentCategoryId);

-- Αναμενόμενο αποτέλεσμα 0: κανένα είδος σε ανύπαρκτη/ξένη κατηγορία.
SELECT COUNT_BIG(*) AS InvalidCategoryItems
FROM dbo.MenuDraftItems AS item
LEFT JOIN dbo.MenuDraftCategories AS category
    ON category.TenantId = item.TenantId
   AND category.MenuId = item.MenuId
   AND category.Id = item.MenuCategoryId
WHERE item.MenuCategoryId IS NOT NULL AND category.Id IS NULL;

-- Αναμενόμενο αποτέλεσμα 0: κανένα είδος χωρίς πραγματικό MenuDraft.
SELECT COUNT_BIG(*) AS OrphanMenuItems
FROM dbo.MenuDraftItems AS item
LEFT JOIN dbo.MenuDrafts AS draft
    ON draft.TenantId = item.TenantId
   AND draft.MenuId = item.MenuId
WHERE draft.MenuId IS NULL;

-- Αναμενόμενο αποτέλεσμα 0: η αναρίθμηση έδωσε μοναδικές θέσεις ανά sibling group.
SELECT TOP (100)
       TenantId, MenuId, ParentCategoryId, Position, COUNT_BIG(*) AS Duplicates
FROM dbo.MenuDraftCategories
GROUP BY TenantId, MenuId, ParentCategoryId, Position
HAVING COUNT_BIG(*) > 1
ORDER BY TenantId, MenuId, ParentCategoryId, Position;

-- Αναμενόμενο αποτέλεσμα 0: κανένα διπλό είδος στο ίδιο επίπεδο, περιλαμβανομένης της ρίζας.
SELECT TOP (100)
       TenantId, MenuId, MenuCategoryId, CatalogItemId, COUNT_BIG(*) AS Duplicates
FROM dbo.MenuDraftItems
GROUP BY TenantId, MenuId, MenuCategoryId, CatalogItemId
HAVING COUNT_BIG(*) > 1
ORDER BY TenantId, MenuId, MenuCategoryId, CatalogItemId;

-- Έλεγχος ενεργών/trusted FK και check constraints.
SELECT fk.name AS ConstraintName, OBJECT_NAME(fk.parent_object_id) AS TableName,
       fk.is_disabled AS IsDisabled, fk.is_not_trusted AS IsNotTrusted
FROM sys.foreign_keys AS fk
WHERE fk.name IN
(
    N'FK_MenuDraftCategories_MenuDraftCategories_TenantId_MenuId_ParentCategoryId',
    N'FK_MenuDraftItems_MenuDrafts_TenantId_MenuId',
    N'FK_MenuDraftItems_MenuDraftCategories_TenantId_MenuId_MenuCategoryId'
)
ORDER BY fk.name;

SELECT cc.name AS ConstraintName,
       cc.is_disabled AS IsDisabled, cc.is_not_trusted AS IsNotTrusted
FROM sys.check_constraints AS cc
WHERE cc.name = N'CK_MenuDraftCategories_NotOwnParent';

-- Επαλήθευση φίλτρων και μοναδικότητας στα νέα indexes.
SELECT OBJECT_NAME(i.object_id) AS TableName, i.name AS IndexName,
       i.is_unique AS IsUnique, i.is_disabled AS IsDisabled,
       i.filter_definition AS FilterDefinition
FROM sys.indexes AS i
WHERE i.name IN
(
    N'IX_MenuDraftCategories_TenantId_MenuId_ParentCategoryId_Position_Id',
    N'IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_CatalogItemId',
    N'IX_MenuDraftItems_TenantId_MenuId_CatalogItemId',
    N'IX_MenuDraftItems_TenantId_MenuId_MenuCategoryId_Position_Id'
)
ORDER BY TableName, i.name;

-- Τέλος: κανένα UPDATE, INSERT, DELETE, ALTER ή migration apply.
