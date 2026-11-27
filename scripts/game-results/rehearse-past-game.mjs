#!/usr/bin/env node
// Rehearses a past game's import end to end against the NEW API on a fresh
// LOCAL PostgreSQL, with the two bodies AMP Vision prepared for it:
//   1. POST /organizations/{orgId}/past-games   (<game>.past_game.json)
//   2. PUT  /events/{eventId}/result            (<game>.game_result.json, eventId filled in)
// then reads the recap, a player's stats and the leaderboard, and checks that
// nothing was sent to anyone (no Notifications row, no push token touched).
//
// The organization is created with the org id given as --org
// and every roster account with the user id the body names (names are fakes:
// this database never sees real names except the ones inside the result body).
// Never points at production.
//
//   node scripts/game-results/rehearse-past-game.mjs --new <new publish dir> \
//     --pg "Host=127.0.0.1;Port=55433;Username=postgres" --psql /opt/homebrew/opt/postgresql@16/bin/psql \
//     --org <orgId> --past <past_game.json> --result <game_result.json>
import { spawn, execFileSync } from 'node:child_process';
import { setTimeout as sleep } from 'node:timers/promises';
import fs from 'node:fs';
import path from 'node:path';

const args = Object.fromEntries(process.argv.slice(2).reduce((acc, a, i, all) =>
  a.startsWith('--') ? [...acc, [a.slice(2), all[i + 1]]] : acc, []));
const pgPort = /Port=(\d+)/.exec(args.pg)[1];
const DB = `gr_rehearse_${Date.now()}`;
const PORT = 5872, BASE = `http://127.0.0.1:${PORT}`;
let failures = 0;
const ok = (c, w) => { console.log(`${c ? 'PASS' : 'FAIL'}  ${w}`); if (!c) failures++; };
const psql = (sql, db = DB) => execFileSync(args.psql, ['-h', '127.0.0.1', '-p', pgPort, '-U', 'postgres', '-d', db, '-tAc', sql], { encoding: 'utf8' }).trim();

async function call(method, url, token, body) {
  const r = await fetch(`${BASE}/api${url}`, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await r.text(); let json = null; try { json = JSON.parse(text); } catch { /* */ }
  return { status: r.status, json, text };
}

async function main() {
  const past = JSON.parse(fs.readFileSync(args.past, 'utf8'));
  const result = JSON.parse(fs.readFileSync(args.result, 'utf8'));
  psql(`CREATE DATABASE ${DB}`, 'postgres');
  const env = { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production', ASPNETCORE_URLS: `http://127.0.0.1:${PORT}`,
    ConnectionStrings__DefaultConnection: `${args.pg};Database=${DB}`, Jwt__Secret: 'local-rehearsal-secret-0123456789abcdef', Jwt__Issuer: 'r', Jwt__Audience: 'r' };
  delete env.DATABASE_URL;
  const api = spawn('dotnet', [path.join(path.resolve(args.new), 'BHMHockey.Api.dll')], { env });
  let up = false;
  for (let i = 0; i < 200 && !up; i++) { try { up = (await fetch(`${BASE}/health`)).status === 200; } catch { /* */ } if (!up) await sleep(300); }
  ok(up, 'NEW API up on a fresh database');

  // the publisher (an org admin), the org with its production id, the roster's accounts
  await call('POST', '/auth/register', null, { email: 'publisher@rehearsal.test', password: 'Passw0rd!x', firstName: 'Test', lastName: 'Publisher' });
  const pubId = psql(`SELECT "Id" FROM "Users" WHERE "Email"='publisher@rehearsal.test'`);
  psql(`INSERT INTO "Organizations" ("Id","Name","CreatorId","IsActive","CreatedAt","UpdatedAt","DefaultStartAsDraft","IsPrivate","DefaultShowWaitlistBeforePublish")
        VALUES ('${args.org}','Sample League (rehearsal)','${pubId}',true,now(),now(),false,false,false)`);
  psql(`INSERT INTO "OrganizationAdmins" ("Id","OrganizationId","UserId","AddedAt") VALUES (gen_random_uuid(),'${args.org}','${pubId}',now())`);
  const ids = [...new Set([...past.roster.map(r => r.userId), ...result.players.filter(p => p.userId).map(p => p.userId)])];
  ids.forEach((id, i) => psql(`INSERT INTO "Users" ("Id","Email","PasswordHash","FirstName","LastName","Role","IsActive","IsGhostPlayer","CreatedAt","UpdatedAt","PushToken")
        VALUES ('${id}','p${i}@rehearsal.test','x','Player','${i}','Player',true,false,now(),now(),'ExponentPushToken[rehearsal-${i}]')`));
  ids.forEach(id => psql(`INSERT INTO "OrganizationSubscriptions" ("Id","OrganizationId","UserId","NotificationEnabled","SubscribedAt") VALUES (gen_random_uuid(),'${args.org}','${id}',true,now())`));
  const token = (await call('POST', '/auth/login', null, { email: 'publisher@rehearsal.test', password: 'Passw0rd!x' })).json.token;
  ok(ids.length === past.roster.length, `${ids.length} roster accounts created with the body's user ids`);

  const pg = await call('POST', `/organizations/${args.org}/past-games`, token, past);
  ok(pg.status === 200 && pg.json.outcome === 'created' && pg.json.registeredCount === past.roster.length && pg.json.skippedUserIds.length === 0,
     `past game created: ${pg.status} ${pg.text}`);
  const eventId = pg.json.eventId;
  ok(psql(`SELECT count(*) FROM "Notifications"`) === '0', 'no notification-center row was written');
  ok(psql(`SELECT "Status"||'/'||"Visibility"||'/'||"IsRosterPublished"||'/'||"Cost" FROM "Events" WHERE "Id"='${eventId}'`) === 'Published/OrganizationMembers/true/0',
     'the event is Published, OrganizationMembers, roster published, free');

  const body = { ...result, eventId };
  const put = await call('PUT', `/events/${eventId}/result`, token, body);
  ok(put.status === 200 && put.json.outcome === 'created', `result accepted: ${put.status} ${put.text}`);
  const again = await call('PUT', `/events/${eventId}/result`, token, body);
  ok(again.json?.outcome === 'unchanged', 'the same file again is unchanged');

  const recap = await call('GET', `/events/${eventId}/result`, token);
  ok(recap.status === 200, 'recap reads');
  const r = recap.json;
  ok(r.black.score === result.teams.black.score && r.white.score === result.teams.white.score, `final ${r.black.label} ${r.black.score} - ${r.white.score} ${r.white.label}`);
  ok(r.goals.length === result.goals.length && r.stars.length === result.stars.length, `${r.goals.length} goals, ${r.stars.length} stars`);
  ok(r.blackLines.length + r.whiteLines.length === result.players.length, `${r.blackLines.length + r.whiteLines.length} box-score lines`);
  const linked = result.players.find(p => p.userId);
  const stats = await call('GET', `/users/${linked.userId}/stats?season=2026`, token);
  ok(stats.status === 200 && stats.json.gameLog.length === 1, `a linked player's stats: ${JSON.stringify(stats.json?.seasonLine)}`);
  // The registrations query runs on PostgreSQL: this game has stats, so it is not repeated as "No stats"
  ok(Array.isArray(stats.json?.gamesWithoutStats) && stats.json.gamesWithoutStats.length === 0,
    `no-stats games: ${JSON.stringify(stats.json?.gamesWithoutStats)}`);
  const lb = await call('GET', `/organizations/${args.org}/leaderboard?season=2026&stat=points`, token);
  ok(lb.status === 200 && lb.json.rows.length === result.players.filter(p => p.userId).length, `leaderboard: ${lb.json?.rows.length} linked players ranked`);
  ok(psql(`SELECT count(*) FROM "Notifications"`) === '0', 'still no notification after the result');

  api.kill('SIGTERM');
  console.log(failures ? `\n${failures} FAILED` : '\nALL PASSED');
  process.exit(failures ? 1 : 0);
}
main().catch(e => { console.error(e); process.exit(2); });
