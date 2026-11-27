START TRANSACTION;


DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    DROP TABLE "GameGoals";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    DROP TABLE "GamePlayerLines";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    DROP TABLE "GameResults";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    ALTER TABLE "Users" DROP COLUMN "HideGameStats";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20261127194158_AddGameResults';
    END IF;
END $EF$;
COMMIT;

