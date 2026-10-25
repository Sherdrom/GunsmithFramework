const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');

const source = fs.readFileSync('GunsmithAnchorEditor/index.html', 'utf8');
const loading = source.slice(source.indexOf('    async function openProject()'), source.indexOf('    function loadM4Example()'));
const originalParts = [{ id: 'project_A' }];
const originalHandle = { name: 'project_A.json' };
const state = { parts: originalParts, projectHandle: originalHandle, projectName: originalHandle.name, preview: {} };
let nextFile;
const newHandle = { name: 'project_B.json', getFile: async () => nextFile };
let remembered;
let status;
const context = {
  state, window: { showOpenFilePicker: true }, el: { backgroundColor: {} },
  showJsonOpenPicker: async () => [newHandle],
  rememberPickerHandle: async (_, handle) => { remembered = handle; },
  setProjectStatus: message => { status = message; },
  applyProjectRootFromJsonFile: async () => null,
  migrateProjectImages: data => data.images || [],
  loadImageEntries: (images, done) => { state.images = images; done(); },
  normalizePart: part => part,
  refreshImageSelects() {}, imageLoadStatusSuffix: () => '', draw() {}, fitImage() {}
};
vm.createContext(context);
vm.runInContext(loading, context);

(async () => {
  for (const text of ['{invalid', '{}', '{"parts":[null],"canvas":{"w":64,"h":64},"rootSocket":{"x":0,"y":0}}']) {
    nextFile = { name: newHandle.name, text: async () => text };
    await context.openProject();
    assert.equal(state.parts, originalParts);
    assert.equal(state.projectHandle, originalHandle);
    assert.equal(state.projectName, originalHandle.name);
    assert.equal(remembered, undefined);
    assert.match(status, /打开失败/);
  }
  nextFile = { name: newHandle.name, text: async () => { throw new Error('read failed'); } };
  await context.openProject();
  assert.equal(state.projectHandle, originalHandle);
  assert.equal(state.parts, originalParts);

  const project = { parts: [], canvas: { w: 64, h: 64 }, rootSocket: { x: 0, y: 0 }, images: [] };
  nextFile = { name: newHandle.name, text: async () => JSON.stringify(project) };
  await context.openProject();
  assert.equal(state.projectHandle, newHandle);
  assert.equal(state.projectName, newHandle.name);
  assert.equal(state.parts.length, 0);
  assert.equal(remembered, newHandle);
  assert.match(status, /已打开/);

  await context.loadProject(nextFile);
  assert.equal(state.projectHandle, null, 'file-input imports must not retain an old writable handle');
  console.log('Anchor editor preserves the active project on failed reads and invalid imports');
})().catch(error => { console.error(error); process.exitCode = 1; });
