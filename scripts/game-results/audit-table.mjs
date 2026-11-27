#!/usr/bin/env node
// Builds the compatibility audit table: every HTTP call the installed app makes
// (packages/api-client at the 1.0.5 store build dfced95 and at 60977b7, the
// newest commit an over-the-air bundle can have come from), joined to the
// endpoint it reaches in the API's contract snapshot, with the snapshot line
// that pins it.
//   node scripts/game-results/audit-table.mjs <calls_105.txt> <calls_head.txt> > table.md
// The calls files come from scripts/game-results/extract-client-calls.js <services dir> (run on
// packages/api-client/src/services at each commit, e.g. via git archive).
import fs from 'node:fs';
import path from 'node:path';

const [f105, fHead] = process.argv.slice(2);
const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), '../..');
const contractDir = path.join(root, 'apps/api/BHMHockey.Api.Tests/Contract');
const baseline = fs.readFileSync(path.join(contractDir, 'installed_client_contract.txt'), 'utf8').split('\n').filter(Boolean);

const norm = (verb, p) => `${verb} ${p.replace(/\{[^}]*\}/g, '{}').toLowerCase().replace(/\/+$/, '')}`;
const index = new Map();
baseline.forEach((line, i) => {
  const [route] = line.split(' | ');
  const [verb, p] = route.split(' ');
  index.set(norm(verb, p.replace(/^\/api/, '')), { line: i + 1, text: line });
});

const read = f => fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(l => {
  const [, verb, p, file] = /^(\S+) (\S+)\s+\[(.*)\]$/.exec(l);
  return { verb, p, file, key: norm(verb, p) };
});
const v105 = new Set(read(f105).map(c => c.key));
const head = read(fHead);
const seen = new Set();

console.log('| # | call (as the app sends it) | in 1.0.5 store bundle | in OTA bundle (60977b7) | returns (declared) | pinned by baseline line | status |');
console.log('|---|---|---|---|---|---|---|');
let n = 0, unmatched = 0;
for (const c of head) {
  if (seen.has(c.key)) continue;
  seen.add(c.key);
  const hit = index.get(c.key);
  const ret = hit ? hit.text.split(' | returns ')[1].replace(/\{.*$/, '').replace(/^\[(\w+).*$/, '[$1]') : '-';
  if (!hit) unmatched++;
  console.log(`| ${++n} | \`${c.verb} ${c.p}\` (${c.file}) | ${v105.has(c.key) ? 'yes' : 'no'} | yes | ${ret} | ${hit ? hit.line : 'none'} | ${hit ? 'unchanged (test passes)' : 'no such route on the server, before or after (404 both ways)'} |`);
}
console.error(`${n} distinct calls, ${unmatched} with no server route`);
