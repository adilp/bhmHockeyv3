#!/bin/bash
#
# Badge Assignment Script for BHM Hockey
# Usage: ./assign-badge.sh
#
# Assigns badges to users via direct database insert.
# Requires psql to be installed.
#

set -e

# Database connection (defaults for local dev)
DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5433}"
DB_NAME="${DB_NAME:-bhmhockey}"
DB_USER="${DB_USER:-bhmhockey}"
DB_PASSWORD="${DB_PASSWORD:-password}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}=== BHM Hockey Badge Assignment ===${NC}\n"

# Check for psql
if ! command -v psql &> /dev/null; then
    echo -e "${RED}Error: psql is not installed. Please install PostgreSQL client.${NC}"
    exit 1
fi

# Build connection string
export PGPASSWORD="$DB_PASSWORD"
PSQL_CMD="psql -h $DB_HOST -p $DB_PORT -d $DB_NAME -U $DB_USER -t -A"

# Function to run SQL and return result
run_sql() {
    echo "$1" | $PSQL_CMD 2>/dev/null
}

# List available badge types
echo -e "${YELLOW}Available Badge Types:${NC}"
echo "-----------------------------------"
run_sql "SELECT \"Code\", \"Name\" FROM \"BadgeTypes\" ORDER BY \"SortPriority\";" | while IFS='|' read -r code name; do
    echo "  $code - $name"
done
echo ""

# Get badge type
echo -e "${YELLOW}Enter badge type code:${NC}"
read -r BADGE_CODE

# Validate badge type
BADGE_ID=$(run_sql "SELECT \"Id\" FROM \"BadgeTypes\" WHERE \"Code\" = '$BADGE_CODE';")
if [ -z "$BADGE_ID" ]; then
    echo -e "${RED}Error: Invalid badge type '$BADGE_CODE'${NC}"
    exit 1
fi

BADGE_NAME=$(run_sql "SELECT \"Name\" FROM \"BadgeTypes\" WHERE \"Code\" = '$BADGE_CODE';")
echo -e "${GREEN}Selected: $BADGE_NAME${NC}\n"

# Get user email
echo -e "${YELLOW}Enter user email (or comma-separated list for bulk):${NC}"
read -r USER_EMAILS

# Get context based on badge type
echo ""
if [ "$BADGE_CODE" = "tournament_winner" ]; then
    echo -e "${YELLOW}Enter tournament name:${NC}"
    read -r TOURNAMENT_NAME
    echo -e "${YELLOW}Enter tournament year:${NC}"
    read -r TOURNAMENT_YEAR
    CONTEXT="{\"tournamentName\": \"$TOURNAMENT_NAME\", \"year\": $TOURNAMENT_YEAR}"
elif [ "$BADGE_CODE" = "beta_tester" ]; then
    CONTEXT='{"description": "Original beta tester"}'
    echo -e "${BLUE}Using default context: $CONTEXT${NC}"
else
    echo -e "${YELLOW}Enter context JSON (or press Enter for empty):${NC}"
    read -r CONTEXT
    if [ -z "$CONTEXT" ]; then
        CONTEXT='{}'
    fi
fi

# Get earned date
echo ""
echo -e "${YELLOW}Enter earned date (YYYY-MM-DD) or press Enter for today:${NC}"
read -r EARNED_DATE
if [ -z "$EARNED_DATE" ]; then
    EARNED_DATE=$(date +%Y-%m-%d)
fi

echo ""
echo -e "${BLUE}Summary:${NC}"
echo "-----------------------------------"
echo "Badge: $BADGE_NAME ($BADGE_CODE)"
echo "Users: $USER_EMAILS"
echo "Context: $CONTEXT"
echo "Earned: $EARNED_DATE"
echo ""

echo -e "${YELLOW}Proceed? (y/n)${NC}"
read -r CONFIRM
if [ "$CONFIRM" != "y" ] && [ "$CONFIRM" != "Y" ]; then
    echo "Cancelled."
    exit 0
fi

# Process each email
echo ""
IFS=',' read -ra EMAILS <<< "$USER_EMAILS"
SUCCESS_COUNT=0
FAIL_COUNT=0

for EMAIL in "${EMAILS[@]}"; do
    # Trim whitespace
    EMAIL=$(echo "$EMAIL" | xargs)

    # Get user ID
    USER_ID=$(run_sql "SELECT \"Id\" FROM \"Users\" WHERE \"Email\" = '$EMAIL';")

    if [ -z "$USER_ID" ]; then
        echo -e "${RED}User not found: $EMAIL${NC}"
        ((FAIL_COUNT++))
        continue
    fi

    # Check if user already has this badge
    EXISTING=$(run_sql "SELECT \"Id\" FROM \"UserBadges\" WHERE \"UserId\" = '$USER_ID' AND \"BadgeTypeId\" = '$BADGE_ID';")
    if [ -n "$EXISTING" ]; then
        echo -e "${YELLOW}User already has badge: $EMAIL${NC}"
        ((FAIL_COUNT++))
        continue
    fi

    # Insert badge
    run_sql "INSERT INTO \"UserBadges\" (\"Id\", \"UserId\", \"BadgeTypeId\", \"Context\", \"EarnedAt\", \"DisplayOrder\")
             VALUES (gen_random_uuid(), '$USER_ID', '$BADGE_ID', '$CONTEXT'::jsonb, '$EARNED_DATE', NULL);"

    if [ $? -eq 0 ]; then
        echo -e "${GREEN}Badge assigned to: $EMAIL${NC}"
        ((SUCCESS_COUNT++))
    else
        echo -e "${RED}Failed to assign badge to: $EMAIL${NC}"
        ((FAIL_COUNT++))
    fi
done

echo ""
echo -e "${BLUE}=== Complete ===${NC}"
echo -e "Success: ${GREEN}$SUCCESS_COUNT${NC}"
echo -e "Failed: ${RED}$FAIL_COUNT${NC}"
