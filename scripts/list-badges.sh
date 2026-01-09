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
            bt.name as badge,
            ub.context->>'tournamentName' as tournament,
            to_char(ub.earned_at, 'Mon DD, YYYY') as earned
        FROM user_badges ub
        JOIN badge_types bt ON ub.badge_type_id = bt.id
        JOIN users u ON ub.user_id = u.id
        WHERE u.email = '$EMAIL'
        ORDER BY ub.earned_at DESC;
    "
else
    # Show all users with badges
    echo -e "${BLUE}=== All Users with Badges ===${NC}\n"

    $PSQL_CMD -c "
        SELECT
            u.email,
            u.first_name || ' ' || u.last_name as name,
            COUNT(ub.id) as badge_count,
            STRING_AGG(bt.name, ', ' ORDER BY ub.earned_at) as badges
        FROM users u
        JOIN user_badges ub ON u.id = ub.user_id
        JOIN badge_types bt ON ub.badge_type_id = bt.id
        GROUP BY u.id, u.email, u.first_name, u.last_name
        ORDER BY badge_count DESC, u.last_name;
    "
fi
