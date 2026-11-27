#!/bin/bash
# Proves docs/game-results/AddGameResults.{down,up}.sql on a LOCAL copy of a
# migrated database: down removes exactly the new tables and column, keeps
# every existing row, and is safe twice; up re-applies and is safe twice.
# Usage: scripts/game-results/rollback-sql-check.sh <migrated_local_db> [psql] [port]
set -euo pipefail
SRC=$1
PSQL=${2:-/opt/homebrew/opt/postgresql@16/bin/psql}
PORT=${3:-55433}
DIR=$(cd "$(dirname "$0")/../.." && pwd)
P() { "$PSQL" -h 127.0.0.1 -p "$PORT" -U postgres -v ON_ERROR_STOP=1 -q "$@"; }
T="rb_test_$(date +%s)"
P -d postgres -c "CREATE DATABASE $T TEMPLATE $SRC"
users_before=$(P -d "$T" -tAc 'select count(*) from "Users"')
events_before=$(P -d "$T" -tAc 'select count(*) from "Events"')
P -d "$T" -f "$DIR/docs/game-results/AddGameResults.down.sql"
echo "after down: tables=$(P -d "$T" -tAc "select count(*) from information_schema.tables where table_name in ('GameResults','GamePlayerLines','GameGoals')") column=$(P -d "$T" -tAc "select count(*) from information_schema.columns where table_name='Users' and column_name='HideGameStats'") users=$(P -d "$T" -tAc 'select count(*) from "Users"')/$users_before events=$(P -d "$T" -tAc 'select count(*) from "Events"')/$events_before"
P -d "$T" -f "$DIR/docs/game-results/AddGameResults.down.sql"
echo "down twice: ok"
P -d "$T" -f "$DIR/docs/game-results/AddGameResults.up.sql"
P -d "$T" -f "$DIR/docs/game-results/AddGameResults.up.sql"
echo "up twice: tables=$(P -d "$T" -tAc "select count(*) from information_schema.tables where table_name in ('GameResults','GamePlayerLines','GameGoals')") last_migration=$(P -d "$T" -tAc 'select "MigrationId" from "__EFMigrationsHistory" order by 1 desc limit 1')"
echo "database $T left for inspection"
