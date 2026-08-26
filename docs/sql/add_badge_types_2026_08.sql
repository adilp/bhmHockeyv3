-- New badge types (Aug 2026). Icons are registered in
-- apps/mobile/components/badges/BadgeIcon.tsx - the IconName below MUST match
-- an iconMap key or the badge renders with no image.
-- Ship the app update containing the icons BEFORE running this in production.
INSERT INTO "BadgeTypes" ("Id", "Code", "Name", "Description", "IconName", "Category", "SortPriority", "CreatedAt")
VALUES
  (gen_random_uuid(), 'FIGHTS_CANCER', 'Hockey Fights Cancer',
   'Played in one or more Birmingham Hockey Fights Cancer Game.',
   'fights_cancer', 'special', 20, NOW()),
  (gen_random_uuid(), 'GOALIE', 'Goalie',
   'Played Goalie in one or more AMP games.',
   'goalie', 'achievement', 21, NOW()),
  (gen_random_uuid(), 'IRONMAN', 'Ironman',
   'Played in one or more AMP Ironman games.',
   'ironman', 'achievement', 22, NOW()),
  (gen_random_uuid(), 'SHUTOUT', 'Shutout',
   'Played Goalie and got a shutout in one or more AMP games.',
   'shutout', 'achievement', 23, NOW()),
  (gen_random_uuid(), 'FIRST_BLOOD', 'First Blood',
   'As a result of an injury received, drew first blood in an AMP game.',
   'first_blood', 'special', 24, NOW())
ON CONFLICT ("Code") DO NOTHING;
