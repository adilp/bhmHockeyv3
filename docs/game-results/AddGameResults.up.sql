START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    SET LOCAL lock_timeout = '5s';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    ALTER TABLE "Users" ADD "HideGameStats" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE TABLE "GameResults" (
        "Id" uuid NOT NULL,
        "EventId" uuid NOT NULL,
        "OrganizationId" uuid NOT NULL,
        "Status" character varying(20) NOT NULL,
        "BlackScore" integer NOT NULL,
        "WhiteScore" integer NOT NULL,
        "BlackLabel" character varying(50),
        "WhiteLabel" character varying(50),
        "FinalScoreSource" character varying(20) NOT NULL,
        "PeriodCount" integer,
        "PeriodLengthSec" integer,
        "VideoUrl" character varying(500),
        "VideoChaptersJson" jsonb,
        "MeasurementJson" jsonb,
        "NotesJson" jsonb,
        "EstimateDisclaimer" text,
        "Source" character varying(50) NOT NULL,
        "SourceGameKey" character varying(100) NOT NULL,
        "SourceRevision" integer NOT NULL,
        "SourceCodeVersion" character varying(64),
        "SourceGeneratedAt" timestamp with time zone NOT NULL,
        "PayloadHash" character varying(64) NOT NULL,
        "PublishedByUserId" uuid NOT NULL,
        "PublishedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "WithdrawnAt" timestamp with time zone,
        "WithdrawnReason" character varying(500),
        CONSTRAINT "PK_GameResults" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_GameResults_Events_EventId" FOREIGN KEY ("EventId") REFERENCES "Events" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_GameResults_Organizations_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES "Organizations" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE TABLE "GamePlayerLines" (
        "Id" uuid NOT NULL,
        "GameResultId" uuid NOT NULL,
        "SourcePlayerKey" character varying(64) NOT NULL,
        "AmpPlayerId" integer,
        "UserId" uuid,
        "DisplayName" character varying(200) NOT NULL,
        "Team" character varying(10) NOT NULL,
        "Position" character varying(10) NOT NULL,
        "Jersey" character varying(4),
        "IsLoaner" boolean NOT NULL,
        "Goals" integer,
        "Assists" integer,
        "Points" integer,
        "Star" integer,
        "GoalsAgainst" integer,
        "Saves" integer,
        "EstimatesJson" jsonb,
        CONSTRAINT "PK_GamePlayerLines" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_GamePlayerLines_GameResults_GameResultId" FOREIGN KEY ("GameResultId") REFERENCES "GameResults" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_GamePlayerLines_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE TABLE "GameGoals" (
        "Id" uuid NOT NULL,
        "GameResultId" uuid NOT NULL,
        "Sequence" integer NOT NULL,
        "Team" character varying(10) NOT NULL,
        "Period" integer,
        "PeriodElapsedSec" integer,
        "BlackScoreAfter" integer NOT NULL,
        "WhiteScoreAfter" integer NOT NULL,
        "ScorerLineId" uuid,
        "ScorerSource" character varying(30),
        "PrimaryAssistLineId" uuid,
        "SecondaryAssistLineId" uuid,
        "AssistsSource" character varying(30) NOT NULL,
        "VideoOffsetSec" integer,
        CONSTRAINT "PK_GameGoals" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_GameGoals_GamePlayerLines_PrimaryAssistLineId" FOREIGN KEY ("PrimaryAssistLineId") REFERENCES "GamePlayerLines" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GameGoals_GamePlayerLines_ScorerLineId" FOREIGN KEY ("ScorerLineId") REFERENCES "GamePlayerLines" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GameGoals_GamePlayerLines_SecondaryAssistLineId" FOREIGN KEY ("SecondaryAssistLineId") REFERENCES "GamePlayerLines" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GameGoals_GameResults_GameResultId" FOREIGN KEY ("GameResultId") REFERENCES "GameResults" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE UNIQUE INDEX "IX_GameGoals_GameResultId_Sequence" ON "GameGoals" ("GameResultId", "Sequence");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE INDEX "IX_GameGoals_PrimaryAssistLineId" ON "GameGoals" ("PrimaryAssistLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE INDEX "IX_GameGoals_ScorerLineId" ON "GameGoals" ("ScorerLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE INDEX "IX_GameGoals_SecondaryAssistLineId" ON "GameGoals" ("SecondaryAssistLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE INDEX "IX_GamePlayerLines_AmpPlayerId" ON "GamePlayerLines" ("AmpPlayerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE UNIQUE INDEX "IX_GamePlayerLines_GameResultId_SourcePlayerKey" ON "GamePlayerLines" ("GameResultId", "SourcePlayerKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE INDEX "IX_GamePlayerLines_UserId" ON "GamePlayerLines" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE UNIQUE INDEX "IX_GameResults_EventId" ON "GameResults" ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE INDEX "IX_GameResults_OrganizationId_Status" ON "GameResults" ("OrganizationId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    CREATE UNIQUE INDEX "IX_GameResults_Source_SourceGameKey" ON "GameResults" ("Source", "SourceGameKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261127194158_AddGameResults') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261127194158_AddGameResults', '8.0.0');
    END IF;
END $EF$;
COMMIT;

