#!/usr/bin/env node
// Startup and compatibility check for the game-results deploy, against a REAL
// PostgreSQL on localhost. Never points at production.
//
// What it proves (each step prints PASS/FAIL; exit 1 on any FAIL):
//  A. The OLD API (the build production runs today) migrates an empty database
//     and serves; seed data is created through its own endpoints.
//  B. Every GET the installed app makes, answered by the OLD API, is recorded.
//  C. The NEW API starts on that same database (it applies AddGameResults at
//     startup), /health is 200, and the same GETs return byte-identical JSON.
//  D. The new endpoints work end to end: past game, publish, idempotent retry,
//     recap, player stats, leaderboard, recent results, hide switch.
//  E. The OLD API starts again on the MIGRATED database (a rollback of the code
//     without a rollback of the schema), /health is 200, the same GETs are
//     identical again, and it can still register a user (INSERT into Users
//     without knowing HideGameStats) and create an event.
//     It also replays the NEW app's game-results calls against the OLD API: each
//     must be a non-401 error (the store hides the feature; a 401 signs users out).
//  F. The NEW API restarted is a no-op migration ("No pending migrations").
//  G. A migration that cannot get its lock (another session holds Users) fails
//     within lock_timeout, the NEW API exits instead of serving, and the schema
//     is left exactly as it was; the OLD API still serves on it.
//
// Usage (paths are this machine's; all local):
//   node scripts/game-results/startup-compat-check.mjs \
//     --old <old publish dir> --new <new publish dir> \
//     --pg "Host=127.0.0.1;Port=55433;Username=postgres" --psql /opt/homebrew/opt/postgresql@16/bin/psql
import { spawn, execFileSync } from 'node:child_process';
import { setTimeout as sleep } from 'node:timers/promises';
import path from 'node:path';

const args = Object.fromEntries(process.argv.slice(2).reduce((acc, a, i, all) =>
  a.startsWith('--') ? [...acc, [a.slice(2), all[i + 1]]] : acc, []));
const OLD = path.resolve(args.old), NEW = path.resolve(args.new);
const PG = args.pg, PSQL = args.psql;
const pgPort = /Port=(\d+)/.exec(PG)[1];
const DB = `gr_compat_${Date.now()}`;
const PORT = 5871;
const BASE = `http://127.0.0.1:${PORT}`;
let failures = 0;
const ok = (cond, what) => { console.log(`${cond ? 'PASS' : 'FAIL'}  ${what}`); if (!cond) failures++; };

function psql(sql, db = DB) {
  return execFileSync(PSQL, ['-h', '127.0.0.1', '-p', pgPort, '-U', 'postgres', '-d', db, '-tAc', sql], { encoding: 'utf8' }).trim();
}

function startApi(dir, db = DB) {
  const env = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: 'Production',          // production's startup path: a failed migration rethrows
    ASPNETCORE_URLS: `http://127.0.0.1:${PORT}`,   // localhost only
    ConnectionStrings__DefaultConnection: `${PG};Database=${db}`,
    Jwt__Secret: 'local-compat-check-secret-0123456789abcdef',
    Jwt__Issuer: 'compat', Jwt__Audience: 'compat',
  };
  delete env.DATABASE_URL;
  const p = spawn('dotnet', [path.join(dir, 'BHMHockey.Api.dll')], { env, cwd: dir });
  p.log = '';
  p.stdout.on('data', d => { p.log += d; });
  p.stderr.on('data', d => { p.log += d; });
  p.exited = new Promise(r => p.on('exit', (code, signal) => r(code ?? signal)));
  return p;
}

async function healthy(p, ms = 60000) {
  const t0 = Date.now();
  while (Date.now() - t0 < ms) {
    if (p.exitCode !== null) return false;
    try { const r = await fetch(`${BASE}/health`); if (r.status === 200) return true; } catch { /* not up yet */ }
    await sleep(300);
  }
  return false;
}

async function stop(p) { p.kill('SIGTERM'); await p.exited; await sleep(300); }

async function call(method, url, token, body) {
  const r = await fetch(`${BASE}/api${url}`, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await r.text();
  let json = null; try { json = text ? JSON.parse(text) : null; } catch { /* not json */ }
  return { status: r.status, text, json };
}

async function login(email) {
  const r = await call('POST', '/auth/login', null, { email, password: 'Passw0rd!x' });
  return r.json.token;
}

// The GETs the installed client makes (packages/api-client at 60977b7 and dfced95),
// minus the admin-panel and tournament ones that need data this seed does not make.
function clientGets(s) {
  return [
    ['/users/me', s.adminToken], ['/users/me', s.playerToken],
    ['/users/me/subscriptions', s.playerToken], ['/users/me/registrations', s.playerToken],
    ['/users/me/organizations', s.adminToken], ['/users/me/pending-waivers', s.playerToken],
    ['/users/me/badges/uncelebrated', s.playerToken], ['/users/me/tournament-invitations', s.playerToken],
    ['/users/me/upcoming-tournament-matches', s.playerToken], [`/users/${s.playerId}/badges`, s.adminToken],
    ['/events', s.playerToken], ['/events', null], ['/events/past', s.playerToken], ['/events/past', s.adminToken],
    [`/events/${s.pastEventId}`, s.playerToken], [`/events/${s.futureEventId}`, s.playerToken],
    [`/events/${s.pastEventId}/registrations`, s.adminToken], [`/events/${s.futureEventId}/registrations`, s.playerToken],
    ['/organizations', s.playerToken], [`/organizations/${s.orgId}`, s.playerToken],
    [`/organizations/${s.orgId}/members`, s.adminToken], [`/organizations/${s.orgId}/admins`, s.adminToken],
    [`/organizations/${s.orgId}/auto-roster`, s.adminToken], [`/organizations/${s.orgId}/join-requests`, s.adminToken],
    [`/organizations/${s.orgId}/waiver`, s.playerToken], ['/notifications', s.playerToken],
    ['/notifications/unread-count', s.playerToken], ['/tournaments', s.playerToken],
  ];
}

async function snapshot(s) {
  const out = {};
  for (const [url, tok] of clientGets(s)) {
    const r = await call('GET', url, tok);
    out[`${tok === s.adminToken ? 'admin' : tok ? 'player' : 'anon'} GET ${url}`] = `${r.status} ${r.text}`;
  }
  return out;
}

function compare(a, b, label) {
  const codes = Object.values(a).map(v => v.slice(0, 3)).reduce((m, c) => ({ ...m, [c]: (m[c] || 0) + 1 }), {});
  console.log(`      status codes of the recorded GETs: ${JSON.stringify(codes)}`);
  const diffs = Object.keys(a).filter(k => a[k] !== b[k]);
  ok(diffs.length === 0, `${label}: ${Object.keys(a).length} installed-client GETs byte-identical` +
    (diffs.length ? `\n      differs: ${diffs.join('\n      differs: ')}` : ''));
}

async function main() {
  psql(`CREATE DATABASE ${DB}`, 'postgres');
  console.log(`database ${DB}`);

  // A. old API on an empty database
  let api = startApi(OLD);
  ok(await healthy(api), 'A. OLD API migrates an empty database and /health is 200');
  ok(psql(`SELECT to_regclass('"GameResults"') IS NULL`) === 't', 'A. the old schema has no GameResults table');

  const s = {};
  for (const [email, first] of [['admin@compat.test', 'Ada'], ['player@compat.test', 'Pat']]) {
    const r = await call('POST', '/auth/register', null, { email, password: 'Passw0rd!x', firstName: first, lastName: 'Test', positions: { skater: 'Silver' } });
    if (r.status !== 200) throw new Error(`register ${email}: ${r.status} ${r.text}`);
  }
  psql(`UPDATE "Users" SET "Role"='Admin' WHERE "Email"='admin@compat.test'`);
  s.adminToken = await login('admin@compat.test');
  s.playerToken = await login('player@compat.test');
  s.playerId = (await call('GET', '/users/me', s.playerToken)).json.id;
  const org = await call('POST', '/organizations', s.adminToken, { name: 'Compat Pickup', description: 'local', skillLevels: ['Silver'] });
  s.orgId = org.json.id;
  await call('POST', `/organizations/${s.orgId}/subscribe`, s.playerToken);
  const past = await call('POST', '/events', s.adminToken, {
    eventDate: '2026-11-25T02:00:00Z', maxPlayers: 20, cost: 0, organizationId: s.orgId, name: 'Past game', visibility: 'OrganizationMembers', applyAutoRoster: false, startAsDraft: false });
  s.pastEventId = past.json.id;
  const future = await call('POST', '/events', s.adminToken, {
    eventDate: '2027-06-01T01:00:00Z', maxPlayers: 20, cost: 10, organizationId: s.orgId, name: 'Future game', applyAutoRoster: false, startAsDraft: false });
  s.futureEventId = future.json.id;
  await call('POST', `/events/${s.futureEventId}/register`, s.playerToken, { position: 'Skater' });
  psql(`INSERT INTO "EventRegistrations" ("Id","EventId","UserId","Status","RegisteredAt","RegisteredPosition","TeamAssignment")
        VALUES (gen_random_uuid(), '${s.pastEventId}', '${s.playerId}', 'Registered', now(), 'Skater', 'White')`);
  ok(s.orgId && s.pastEventId && s.futureEventId, 'A. seed data created through the OLD API');

  // B. record
  const before = await snapshot(s);
  await stop(api);

  // C. new API on the old schema: migrates at startup
  api = startApi(NEW);
  ok(await healthy(api), 'C. NEW API starts on the un-migrated database and /health is 200');
  ok(/Pending migrations: 1/.test(api.log) && /AddGameResults/.test(api.log), 'C. it applied exactly one pending migration, AddGameResults');
  ok(psql(`SELECT count(*) FROM information_schema.tables WHERE table_name IN ('GameResults','GamePlayerLines','GameGoals')`) === '3', 'C. the three new tables exist');
  ok(psql(`SELECT column_default FROM information_schema.columns WHERE table_name='Users' AND column_name='HideGameStats'`) === 'false', 'C. Users.HideGameStats has a database default of false');
  ok(psql(`SELECT count(*) FROM "Users" WHERE "HideGameStats"`) === '0', 'C. every existing user reads HideGameStats = false');
  compare(before, await snapshot(s), 'C. NEW API vs OLD API');

  // D. new endpoints
  const pg = await call('POST', `/organizations/${s.orgId}/past-games`, s.adminToken, {
    name: 'Home vs Visitors', eventDate: '2026-11-23T02:15:00Z', duration: 75, sourceGameKey: '2026-11-22_main',
    roster: [{ userId: s.playerId, team: 'White', position: 'Skater' }] });
  ok(pg.status === 200 && pg.json.outcome === 'created', `D. past game created (${pg.status})`);
  ok(psql(`SELECT count(*) FROM "Notifications" WHERE "EventId"='${pg.json?.eventId}'`) === '0', 'D. the past game wrote no notification');
  const again = await call('POST', `/organizations/${s.orgId}/past-games`, s.adminToken, {
    name: 'Home vs Visitors', eventDate: '2026-11-23T02:15:00Z', sourceGameKey: '2026-11-22_main',
    roster: [{ userId: s.playerId, team: 'White', position: 'Skater' }] });
  ok(again.json?.outcome === 'already_exists', 'D. a second past-game call is already_exists');
  const body = {
    schemaVersion: 'amp.game_result.v1',
    source: { system: 'amp_vision', gameKey: '2026-11-22_main', revision: 1, generatedAtUtc: '2026-11-27T18:00:00Z', codeVersion: 'local' },
    eventId: pg.json.eventId,
    teams: { black: { label: 'Visitors', score: 1 }, white: { label: 'Home', score: 1 } },
    finalScoreSource: 'person', periodCount: 3, periodLengthSec: 1020, video: { url: null, chapters: [] },
    goals: [
      { sequence: 1, team: 'black', period: 1, periodElapsedSec: 145, scoreAfter: { black: 1, white: 0 }, scorer: { playerKey: 'amp:73' }, scorerSource: 'person_click', assists: [], assistsSource: 'person_ruled', videoOffsetSec: null },
      { sequence: 2, team: 'white', period: 2, periodElapsedSec: 500, scoreAfter: { black: 1, white: 1 }, scorer: { playerKey: 'amp:18' }, scorerSource: 'person_click', assists: [], assistsSource: 'person_ruled', videoOffsetSec: null },
    ],
    stars: [{ rank: 1, playerKey: 'amp:18' }],
    players: [
      { playerKey: 'amp:18', ampPlayerId: 18, userId: s.playerId, link: 'linked', displayName: 'Pat Test', team: 'white', position: 'Skater', jersey: '18', isLoaner: false, goals: 1, assists: 0, points: 1, star: 1, goalsAgainst: null, saves: null, estimates: { iceTime: { display: 'about 23 min', qualifier: 'about' } }, estimatesWithheld: null },
      { playerKey: 'amp:73', ampPlayerId: 73, userId: null, link: 'unlinked', displayName: 'Sam Sample', team: 'black', position: 'Skater', jersey: '10', isLoaner: false, goals: 1, assists: 0, points: 1, star: null, goalsAgainst: null, saves: null, estimates: null, estimatesWithheld: null },
    ],
    measurement: { savesMeasured: false }, notes: ['Saves not measured'], estimateDisclaimer: 'Estimates, not measurements.' };
  const put = await call('PUT', `/events/${pg.json.eventId}/result`, s.adminToken, body);
  ok(put.status === 200 && put.json.outcome === 'created', `D. result published (${put.status} ${put.json?.outcome})`);
  const put2 = await call('PUT', `/events/${pg.json.eventId}/result`, s.adminToken, body);
  ok(put2.json?.outcome === 'unchanged', 'D. the same body again is unchanged');
  const denied = await call('PUT', `/events/${pg.json.eventId}/result`, s.playerToken, body);
  ok(denied.status === 403, `D. a player cannot publish (${denied.status})`);
  const recap = await call('GET', `/events/${pg.json.eventId}/result`, s.playerToken);
  ok(recap.status === 200 && recap.json.goals.length === 2 && recap.json.goals[0].scorer.userId === null, 'D. recap reads, a name-only scorer has no userId');
  const stats = await call('GET', `/users/${s.playerId}/stats?season=2026`, s.adminToken);
  ok(stats.status === 200 && stats.json.seasonLine.goals === 1, 'D. player stats total on read');
  const lb = await call('GET', `/organizations/${s.orgId}/leaderboard?season=2026`, s.playerToken);
  ok(lb.status === 200 && lb.json.rows.length === 1, 'D. leaderboard ranks the one linked player');
  const recent = await call('GET', `/organizations/${s.orgId}/results`, s.playerToken);
  ok(recent.status === 200 && recent.json.length === 1, 'D. recent results');
  const none = await call('GET', `/events/${s.pastEventId}/result`, s.playerToken);
  ok(none.status === 404, 'D. a game with no result is 404 (what the app shows as "no stats")');
  const hide = await call('PUT', '/users/me/stats-visibility', s.playerToken, { hidden: true });
  ok(hide.status === 200 && hide.json.hidden === true, 'D. the hide switch');
  const meAfter = await call('GET', '/users/me', s.playerToken);
  ok(!('hideGameStats' in meAfter.json), 'D. /users/me does not carry the new column');
  await call('PUT', '/users/me/stats-visibility', s.playerToken, { hidden: false });
  const after = await snapshot(s);
  await stop(api);

  // E. old API on the migrated database
  api = startApi(OLD);
  ok(await healthy(api), 'E. OLD API starts on the MIGRATED database and /health is 200');
  compare(after, await snapshot(s), 'E. OLD API on migrated schema vs NEW API');
  const reg = await call('POST', '/auth/register', null, { email: 'late@compat.test', password: 'Passw0rd!x', firstName: 'Late', lastName: 'Joiner' });
  ok(reg.status === 200, `E. OLD API registers a user on the migrated Users table (${reg.status})`);
  ok(psql(`SELECT "HideGameStats" FROM "Users" WHERE "Email"='late@compat.test'`) === 'f', 'E. the row the OLD API inserted took the default');
  const ev = await call('POST', '/events', s.adminToken, { eventDate: '2027-07-01T01:00:00Z', maxPlayers: 10, cost: 0, organizationId: s.orgId, applyAutoRoster: false, startAsDraft: true });
  ok(ev.status === 200 || ev.status === 201, `E. OLD API creates an event (${ev.status})`);
  // The NEW app on the OLD API (the app update arriving first, or the server rolled
  // back under it): every game-results call must fail in a way the store reads as
  // "nothing" or "error" and hides - never a 401, which the api-client answers by
  // signing the user out, and never a 2xx with some other body.
  const newAppCalls = [
    ['GET', `/events/${pg.json.eventId}/result`],
    ['GET', `/users/${s.playerId}/stats?season=2026`],
    ['GET', `/users/${s.playerId}/stats`],
    ['GET', '/users/me/stats-visibility'],
    ['PUT', '/users/me/stats-visibility', { hidden: true }],
    ['GET', `/organizations/${s.orgId}/leaderboard?stat=points&season=2026`],
    ['GET', `/organizations/${s.orgId}/results?limit=5`],
  ];
  const oldAnswers = [];
  for (const [m, u, b] of newAppCalls) oldAnswers.push(`${m} ${u.replace(/[0-9a-f-]{36}/g, '{id}')} -> ${(await call(m, u, s.playerToken, b)).status}`);
  console.log(`      the OLD API answers the new app's calls: ${oldAnswers.join('; ')}`);
  ok(oldAnswers.every(a => { const c = Number(a.split('-> ')[1]); return c >= 400 && c !== 401; }),
    'E. OLD API answers every new-app call with a non-401 error (the app hides the feature, the user stays signed in)');
  await stop(api);

  // F. new API again: nothing to migrate
  api = startApi(NEW);
  ok(await healthy(api), 'F. NEW API restarts and /health is 200');
  ok(/Pending migrations: 0/.test(api.log), 'F. no pending migrations the second time');
  await stop(api);

  // G. migration blocked by a lock: fails fast, schema untouched, old API fine
  const DB2 = `${DB}_lock`;
  psql(`CREATE DATABASE ${DB2}`, 'postgres');
  api = startApi(OLD, DB2);
  await healthy(api); await stop(api);
  const locker = spawn(PSQL, ['-h', '127.0.0.1', '-p', pgPort, '-U', 'postgres', '-d', DB2, '-c',
    'BEGIN; LOCK TABLE "Users" IN ACCESS SHARE MODE; SELECT pg_sleep(30); COMMIT;']);
  await sleep(1000);
  const t0 = Date.now();
  api = startApi(NEW, DB2);
  const code = await Promise.race([api.exited, sleep(25000).then(() => 'timeout')]);
  const secs = ((Date.now() - t0) / 1000).toFixed(1);
  ok(code !== 'timeout' && code !== 0, `G. NEW API exits (code ${code}) instead of serving when its migration cannot lock Users, after ${secs}s`);
  ok(/lock timeout/i.test(api.log), 'G. the cause logged is the lock timeout');
  if (code === 'timeout') await stop(api);
  locker.kill();
  await sleep(500);
  ok(psql(`SELECT to_regclass('"GameResults"') IS NULL`, DB2) === 't'
     && psql(`SELECT count(*) FROM information_schema.columns WHERE table_name='Users' AND column_name='HideGameStats'`, DB2) === '0'
     && psql(`SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '%AddGameResults'`, DB2) === '0',
     'G. the failed migration rolled back whole: no table, no column, no history row');
  api = startApi(OLD, DB2);
  ok(await healthy(api), 'G. the OLD API still serves on the untouched schema');
  await stop(api);

  console.log(failures ? `\n${failures} FAILED` : '\nALL PASSED');
  console.log(`(databases ${DB} and ${DB2} left for inspection)`);
  process.exit(failures ? 1 : 0);
}

main().catch(e => { console.error(e); process.exit(2); });
