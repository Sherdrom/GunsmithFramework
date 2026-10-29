const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const { spawnSync } = require('node:child_process');
const source = fs.readFileSync('GunsmithAnchorEditor/index.html', 'utf8');
const code = source.slice(source.indexOf('    function luaString('), source.indexOf('    function setMode('));
const text = 'Scope "A"\\\n\t\r\0' + '123 中文';
const part = { id: 'part-id."x', name: text, type: text, parentIndex: null,
  source: { x: 0, y: 0, w: 32, h: 32 }, attachPoint: { x: 0, y: 0 }, order: 1, scale: 1 };
const child = { parentIndex: 0, mountPath: text, name: text, id: text, anchor: { x: 0, y: 0 } };
const context = { applyFields() {}, selectedPart: () => part, imageEntryForPart: () => ({ path: text }),
  state: { parts: [part, child], selected: 0, rootSocket: { x: 0, y: 0 } }, fmt: String, el: { exportText: {} } };
vm.createContext(context);
vm.runInContext(code, context);
for (const parentIndex of [null, 1]) {
  part.parentIndex = parentIndex;
  context.exportLua();
  const script = 'local parts = {}\n' + context.el.exportText.value + '\n' +
    `local p = parts[${context.luaString(part.id)}]\n` +
    'local value = table.concat({p.name, p.type, p.mounts[1].path, p.mounts[1].defaultPart, p.visual.texture}, "|")\n' +
    'for i = 1, #value do io.write(string.format("%02x", string.byte(value, i))) end';
  const result = spawnSync('lua', ['-'], { input: script, encoding: 'utf8' });
  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stdout, Buffer.from([text, text, text, text, '%ModDir%/' + text.replace(/\\/g, '/')].join('|')).toString('hex'));
}
console.log('Lua export parses and round-trips arbitrary string fields and non-identifier keys');
