# Game results: deploy, verify, roll back

Who does what is marked on every step. Nothing in this document has been run
against production; every step was rehearsed locally (section 6).

## 1. What merging `game-results-server` to `main` does

1. **GitHub Actions** (`.github/workflows/ci.yml`) runs the API tests and the
   mobile type-check/tests on the push. **It does not gate the deploy**: App
   Platform deploys on push whether CI passes or not. So the tests must pass on
   the branch BEFORE merging (they do: section 6).
2. **DigitalOcean App Platform** (`.do/app.yaml`, `deploy_on_push: true`)
   builds `apps/api/Dockerfile` and starts a NEW container while the current
   one keeps serving.
3. The new container runs `Database.Migrate()` (`Program.cs`) before serving:
   it applies exactly one migration, `20261127194158_AddGameResults` (three new
   tables, one new column with a default; `docs/GAME_RESULTS_COMPATIBILITY.md` 3).
   It waits at most 5 s for its locks (`lock_timeout`), all in one transaction.
4. `/health` (database ping) turns 200; App Platform moves traffic to the new
   container and stops the old one. Between 3 and 4 the OLD code serves on the
   NEW schema, which was run and checked (compatibility doc, section 4).
5. If the migration fails, the new container exits (Production re-throws), the
   deployment fails its health check, and the old deployment keeps serving on
   the unchanged schema (the migration rolled back whole). Nothing to repair:
   read the deploy log, fix, push again.

Nothing in the app changes on any phone: the new endpoints are called by
nothing until the app update ships, and no existing response changed.

## 2. Before merging (owner)

- [ ] Read the reviewer list (section 8). The owner's decisions are one
      constant each in `GameResultSettings.cs`.
- [ ] Optional but recommended: take a manual backup of the managed database
      in the DigitalOcean console (Databases -> bhmhockey -> Backups). The
      migration only adds, but a backup costs nothing.
- [ ] Open the PR `game-results-server` -> `main` from your own
      checkout (this clone has no GitHub remote on purpose) and let CI run on
      the PR. Merge when it is green.

## 3. Verify it live, without touching data (owner)

Each is a read. `$API` = `https://bhmhockey-mb3md.ondigitalocean.app/api`,
`$TOKEN` = your own token, set in your own shell only (never written to a file).

```bash
curl -s -o /dev/null -w '%{http_code}\n' https://bhmhockey-mb3md.ondigitalocean.app/health    # 200
# The deploy log (App Platform -> api -> Runtime Logs) shows:
#   "Pending migrations: 1   - 20261127194158_AddGameResults"  then  "Database migration completed successfully"
curl -s -H "Authorization: Bearer $TOKEN" "$API/users/me/stats-visibility"                 # {"hidden":false}
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $TOKEN" \
     "$API/events/<any past event id>/result"                                                # 404: route live, nothing published
curl -s -H "Authorization: Bearer $TOKEN" "$API/organizations/<orgId>/results"   # []
```

Then open the installed app and use it as a player would: Home, Events
(Upcoming and Past), one event's Info/Roster/Chat, an organization, Profile,
Alerts. Everything must look exactly as before. Only after this is the server
"live and verified", and a past game may go in (section 4).

## 4. Adding a game that was played but never existed in the app (owner)

Creating an event through the existing endpoints notifies people (a "New Game"
push to every subscriber, auto-roster notices, or the same push on leaving
Draft). A past game goes in through the one silent route instead
(`POST /organizations/<orgId>/past-games`), sent from the owner's own account as
an admin of the organization, with AMP Vision's sender (`scripts/league_publish.py`,
run from AMP Vision):

```bash
# In your own shell only; the sender never writes the token anywhere
export AMP_LEAGUE_BASE_URL="$API" AMP_LEAGUE_TOKEN="$TOKEN"
# 1. the event and its roster: one Published event and its registrations, nothing sent to anyone
python scripts/league_publish.py past-game --file <game>.past_game.json            # dry run: prints what it would send
python scripts/league_publish.py past-game --file <game>.past_game.json --send     # -> {"eventId": "<eventId>", ...}
# 2. its result: re-export with --event-id <eventId>, then
python scripts/league_publish.py result --file <game>.game_result.json --send
# 3. read it back
curl -s -H "Authorization: Bearer $TOKEN" "$API/events/<eventId>/result" | jq '.black, .white, (.goals|length)'
```

A second past-games call for the same organization and start time writes
nothing and returns the same event. The route refuses a future game and one
before the results floor (`GameResultSettings`). Rehearse both files first on a
local database (section 7, `rehearse-past-game.mjs`).

**Cost and payment.** Optional, and never assumed. A body with `cost` above 0 must
state every roster entry's `paymentStatus`: `Verified` (paid and settled: the
player sees "Paid", the organizer's unpaid count leaves them out, nothing prompts
anyone) or `Pending` (owes: the player sees the amount and the pay buttons). No
payment deadline is ever set, so the expired-deadline job cannot select a past
game. Without `cost` the game is free, as before.

**Correcting a past game's cost and payment.** Send the same body again with
`cost`, every entry's `paymentStatus` and `"correctPayment": true`. It changes
the event's cost and each registration's payment and nothing else, and sends
nothing (outcome `payment_corrected`; the same body again is `unchanged`). It is
refused unless the game was added by this route and the roster names exactly its
registered players with their positions. In AMP Vision:
`export_game_result.py --past-game-start ... --past-game-cost <amount>
--past-game-payment all-paid --correct-payment`, then `league_publish.py past-game`
(its dry run prints the cost and payment in words) and `--send`.

## 5. Switch the feature off without a deploy of code (owner)

App Platform -> api -> Settings -> Environment variables: add
`GameResults__ReadsEnabled` = `false` (and/or `GameResults__WritesEnabled` =
`false`). Saving redeploys the same code (~minutes). Every read answers 404,
which the app treats as "no stats" and hides every new section. Remove the
variable to switch back on. Per game: `scripts/league_publish.py withdraw` in
AMP Vision marks one result withdrawn (it reads as absent; rows kept).

## 6. Roll back

In order of preference; each stops at the first that is enough.

1. **One game is wrong** - withdraw it (AMP Vision, `league_publish.py
   withdraw --event-id ... --revision <higher> --reason ...`), or re-send a
   corrected higher revision. No deploy.
2. **The feature misbehaves** - switch reads off (section 5). No code change.
3. **The new code misbehaves** - App Platform -> api -> Deployments -> the
   previous deployment -> Rollback (or revert the merge commit and push). The
   schema stays: the old code was run on the migrated schema and works (it
   ignores the three tables; its INSERTs into `Users` get the column default).
   **Do not drop anything to roll back code.**
4. **The schema itself must go** (last resort, destroys every published
   result): after step 3, run `docs/game-results/AddGameResults.down.sql` with
   `psql` against the production database from the DigitalOcean console. It
   drops the three tables and `Users.HideGameStats` and removes the history row,
   inside one transaction, idempotent. Re-applying is
   `docs/game-results/AddGameResults.up.sql` (or redeploying the new code).
   Both were run twice on a copy of a migrated database
   (`scripts/game-results/rollback-sql-check.sh`): users and events untouched.

## 7. What was rehearsed locally, and how to re-run it

| what | command | result |
|---|---|---|
| API tests | `cd apps/api && dotnet test BHMHockey.Api.Tests` | 1,348 passed (1,289 before game results, all still passing) |
| installed-client contract | `--filter InstalledClientContract` | 137 endpoints unchanged; 9 additive, reviewed |
| startup and compatibility on real PostgreSQL | `node scripts/game-results/startup-compat-check.mjs --old <old publish> --new <new publish> --pg ... --psql ...` | all 35 checks pass (step E also replays the new app's calls on the OLD API: 404, never 401): `docs/game-results/startup-compat-check.out.txt` |
| rollback SQL | `scripts/game-results/rollback-sql-check.sh <migrated local db>` | down/up each twice, existing rows kept |
| a past game's import, end to end | `node scripts/game-results/rehearse-past-game.mjs ... --past <past_game.json> --result <game_result.json> [--correction <past_game.json with cost>]` | 16 checks pass: event created silently, result accepted, recap/stats (incl. the no-stats list, on PostgreSQL)/leaderboard read, no notification; with `--correction`, 23: corrected once then unchanged, cost and every payment as stated, no deadline, the organizer's unpaid count, still no notification |

The old build is `git archive <main before the change> apps/api` published to
a local folder (`dotnet publish -c Release -o <dir>`); the new one is this
branch published the same way. All of it runs offline against a local
PostgreSQL.

The payload fixture (`apps/api/BHMHockey.Api.Tests/Fixtures/synthetic.game_result.json`)
is SYNTHETIC: invented names, ids and numbers in the exporter's exact shape. No
real game's body belongs in this repository; when the exporter's shape changes,
change the fixture by hand.

## 8. What a reviewer must look at

| file | why |
|---|---|
| `apps/api/BHMHockey.Api/Migrations/20261127194158_AddGameResults.cs` | the only change to production's schema: creates and one `AddColumn`, nothing altered; `lock_timeout` |
| `apps/api/BHMHockey.Api/Data/AppDbContext.cs` (`ConfigureGameResults`) | the new tables' keys, cascades (`Event` delete cascades to its result; a deleted `User` leaves a name-only line) |
| `apps/api/BHMHockey.Api/Models/Entities/User.cs` | the one new property on an existing entity; not on `UserDto` |
| `apps/api/BHMHockey.Api/Program.cs` | three registrations and one optional options binding |
| `apps/api/BHMHockey.Api/Controllers/GameResultsController.cs` | every new route; `[Authorize]`; no 401 for a signed-in caller |
| `apps/api/BHMHockey.Api/Services/GameResultService.cs` | the write rules (admin gate, played game, 2026-11-22 floor, validation, revision) and the read visibility rules |
| `apps/api/BHMHockey.Api/Services/PastGameService.cs` | the one writer of Events/EventRegistrations outside EventService; why it sends nothing |
| `apps/api/BHMHockey.Api/Services/GameResultSettings.cs` | the owner's decisions, one constant each |
| `apps/api/BHMHockey.Api.Tests/Contract/*` | the snapshot of the installed client's endpoints, and the reviewed additions |
| `docs/GAME_RESULTS_COMPATIBILITY.md` | the audit |
