// Lists every HTTP call in an api-client services dir as "VERB path [file]" (path params normalized).
const fs = require('fs'), path = require('path');
const dir = process.argv[2];
const out = new Set();
for (const f of fs.readdirSync(dir)) {
  if (!f.endsWith('.ts')) continue;
  const s = fs.readFileSync(path.join(dir, f), 'utf8');
  const re = /instance\s*\.\s*(get|post|put|delete|patch)\s*(<[\s\S]*?>)?\s*\(\s*([`'"])([\s\S]*?)\3/g;
  let m;
  while ((m = re.exec(s))) {
    const p = m[4].replace(/\$\{[^}]*\}/g, '{p}').replace(/\?.*$/, '');
    out.add(m[1].toUpperCase() + ' ' + p + '   [' + f + ']');
  }
}
console.log([...out].sort().join('\n'));
