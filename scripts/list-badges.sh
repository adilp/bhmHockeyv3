#!/bin/bash
#
# List Badges Script for BHM Hockey
# Usage: ./list-badges.sh [email]
#
# Lists all badges or badges for a specific user.
#

set -e

# Database connection (defaults for local dev)
DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5433}"
DB_NAME="${DB_NAME:-bhmhockey}"
DB_USER="${DB_USER:-bhmhockey}"
DB_PASSWORD="${DB_PASSWORD:-password}"

# Colors
BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

export PGPASSWORD="$DB_PASSWORD"
PSQL_CMD="psql -h $DB_HOST -p $DB_PORT -d $DB_NAME -U $DB_USER"

if [ -n "$1" ]; then
    # Show badges for specific user
    EMAIL="$1"
    echo -e "${BLUE}=== Badges for $EMAIL ===${NC}\n"

    $PSQL_CMD -c "
        SELECT
            bt.\"Name\" as badge,
            ub.\"Context\"->>'tournamentName' as tournament,
            to_char(ub.\"EarnedAt\", 'Mon DD, YYYY') as earned
        FROM \"UserBadges\" ub
        JOIN \"BadgeTypes\" bt ON ub.\"BadgeTypeId\" = bt.\"Id\"
        JOIN \"Users\" u ON ub.\"UserId\" = u.\"Id\"
        WHERE u.\"Email\" = '$EMAIL'
        ORDER BY ub.\"EarnedAt\" DESC;
    "
else
    # Show all users with badges
    echo -e "${BLUE}=== All Users with Badges ===${NC}\n"

    $PSQL_CMD -c "
        SELECT
            u.\"Email\" as email,
            u.\"FirstName\" || ' ' || u.\"LastName\" as name,
            COUNT(ub.\"Id\") as badge_count,
            STRING_AGG(bt.\"Name\", ', ' ORDER BY ub.\"EarnedAt\") as badges
        FROM \"Users\" u
        JOIN \"UserBadges\" ub ON u.\"Id\" = ub.\"UserId\"
        JOIN \"BadgeTypes\" bt ON ub.\"BadgeTypeId\" = bt.\"Id\"
        GROUP BY u.\"Id\", u.\"Email\", u.\"FirstName\", u.\"LastName\"
        ORDER BY badge_count DESC, u.\"LastName\";
    "
fi
