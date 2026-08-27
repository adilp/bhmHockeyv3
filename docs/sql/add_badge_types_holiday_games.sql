-- Holiday game badge types (Aug 2026). Icons are registered in
-- apps/mobile/components/badges/BadgeIcon.tsx - the IconName below MUST match
-- an iconMap key or the badge renders with no image.
-- Ship the app update containing the icons BEFORE running this in production.
INSERT INTO "BadgeTypes" ("Id", "Code", "Name", "Description", "IconName", "Category", "SortPriority", "CreatedAt")
VALUES
  (gen_random_uuid(), 'CHRISTMAS_GAME', 'Christmas Game',
   'Played in one or more AMP Christmas games.',
   'christmas', 'special', 25, NOW()),
  (gen_random_uuid(), 'HALLOWEEN_GAME', 'Halloween Game',
   'Played in one or more AMP Halloween games.',
   'halloween', 'special', 26, NOW())
ON CONFLICT ("Code") DO NOTHING;
