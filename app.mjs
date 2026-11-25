import { readCfc } from './cfc.mjs';

const element = (id) => document.getElementById(id);
const fileInput = element('file');
const zone = element('drop-zone');
const groupSearch = element('group-search');
const moveSearch = element('move-search');
const outputButton = element('export');
const dialog = element('remap');
const animationSearch = element('animation-search');
const animationSelect = element('animation');
const changes = new Map();

let cfc;
let groups = [];
let animationOptions = [];
let selectedGroup;
let selectedMove;
let importSequence = 0;

function node(tag, text, className) {
  const result = document.createElement(tag);
  result.textContent = text;
  if (className) result.className = className;
  return result;
}

function status(message, error = false) {
  element('status').textContent = message;
  element('status').classList.toggle('error', error);
}

function showGroups() {
  const query = groupSearch.value.trim().toLowerCase();
  const list = element('groups');
  if (!cfc) {
    list.replaceChildren(node('p', 'Open a CFC to browse movesets.', 'empty'));
    return;
  }
  const matching = groups.filter((group) => `${group.name} ${group.path}`.toLowerCase().includes(query));
  const items = document.createDocumentFragment();
  for (const group of matching) {
    const button = node('button', '', 'group');
    button.type = 'button';
    button.append(node('span', group.name), node('span', group.moves.length, 'group-total'));
    button.setAttribute('aria-pressed', String(group === selectedGroup));
    button.addEventListener('click', () => {
      list.querySelector('[aria-pressed="true"]')?.setAttribute('aria-pressed', 'false');
      button.setAttribute('aria-pressed', 'true');
      selectedGroup = group;
      moveSearch.value = '';
      showMoves();
    });
    items.append(button);
  }
  list.replaceChildren(items);
  if (!matching.length) list.append(node('p', 'No matching movesets.', 'empty'));
}

function showMoves() {
  const area = element('command-content');
  element('moveset-title').textContent = selectedGroup?.name ?? 'Commands';
  element('moveset-path').textContent = selectedGroup?.path ?? '';
  if (!selectedGroup) {
    area.replaceChildren(node('p', 'The animation slots for a selected moveset will appear here.', 'empty'));
    return;
  }

  const search = moveSearch.value.trim().toLowerCase();
  const moves = selectedGroup.moves.filter((move) => {
    const current = cfc.animations.get(changes.get(move.offset) ?? move.animation);
    return `${move.name} ${current}`.toLowerCase().includes(search);
  });
  if (!moves.length) {
    const message = selectedGroup.moves.length ? 'No matching commands.' : 'This moveset has no editable animation slots.';
    area.replaceChildren(node('p', message, 'empty'));
    return;
  }

  const table = node('table', '');
  const head = table.createTHead().insertRow();
  for (const title of ['Command', 'Animation ID', 'Actions']) {
    const cell = node('th', title);
    cell.scope = 'col';
    head.append(cell);
  }
  const body = table.createTBody();
  for (const move of moves) {
    const row = body.insertRow();
    const replacement = changes.get(move.offset);
    row.insertCell().append(node('span', move.name, 'command-name'));
    const animation = row.insertCell();
    animation.append(node('span', cfc.animations.get(replacement ?? move.animation), 'animation-id'));
    const actions = row.insertCell();
    if (replacement !== undefined) {
      animation.append(node('span', 'Edited', 'edited'));
      const reset = node('button', 'Reset', 'text-button');
      reset.type = 'button';
      reset.setAttribute('aria-label', `Reset ${move.name}`);
      reset.addEventListener('click', () => {
        changes.delete(move.offset);
        showMoves();
        showChanges();
        (area.querySelector(`[data-command="${move.offset}"]`) ?? moveSearch).focus();
      });
      actions.append(reset);
    }
    const edit = node('button', 'Edit', 'text-button');
    edit.type = 'button';
    edit.dataset.command = move.offset;
    edit.setAttribute('aria-label', `Edit ${move.name}`);
    edit.addEventListener('click', () => openRemap(move));
    actions.append(edit);
  }
  area.replaceChildren(table);
}

function showChanges() {
  status(`${changes.size} ${changes.size === 1 ? 'change' : 'changes'} ready to export.`);
}

function showAnimationOptions(preferred = animationSelect.value) {
  const query = animationSearch.value.trim().toLowerCase();
  const options = document.createDocumentFragment();
  for (const [offset, name] of animationOptions) {
    if (name.toLowerCase().includes(query)) options.append(new Option(name, offset));
  }
  animationSelect.replaceChildren(options);
  animationSelect.value = preferred;
  element('apply').disabled = !animationSelect.value;
}

function openRemap(move) {
  selectedMove = move;
  element('remap-command').textContent = `${selectedGroup.name} / ${move.name}`;
  animationSearch.value = '';
  showAnimationOptions(changes.get(move.offset) ?? move.animation);
  dialog.showModal();
  animationSearch.focus();
}

async function openFile(file) {
  if (!file) return;
  if (changes.size && !window.confirm('Replace the open CFC and discard the current edits?')) return;
  const sequence = ++importSequence;
  try {
    if (file.size > 32 * 1024 * 1024) throw new Error('Choose a CFC under 32 MB.');
    const next = readCfc(await file.arrayBuffer());
    if (sequence !== importSequence) return;
    cfc = next;
    changes.clear();
    groups = [...cfc.movesets].sort((a, b) => a.name.localeCompare(b.name));
    animationOptions = [...cfc.animations].sort((a, b) => a[1].localeCompare(b[1]));
    selectedGroup = groups.find((group) => group.name === 'p_kiryu_c') ??
      groups.find((group) => group.moves.length) ?? groups[0];
    element('group-count').textContent = groups.length;
    element('filename').textContent = file.name;
    element('file-info').textContent = `${groups.length} movesets · ${Math.round(cfc.byteLength / 1024)} KB`;
    element('browse').textContent = 'Replace file';
    outputButton.disabled = groupSearch.disabled = moveSearch.disabled = false;
    groupSearch.value = moveSearch.value = '';
    showGroups();
    showMoves();
    showChanges();
    const groupList = element('groups');
    const active = groupList.querySelector('[aria-pressed="true"]');
    if (active) groupList.scrollTop = active.offsetTop - groupList.offsetTop;
  } catch (error) {
    if (sequence === importSequence) status(error.message, true);
  }
}

element('browse').addEventListener('click', () => fileInput.click());
fileInput.addEventListener('change', () => {
  openFile(fileInput.files[0]);
  fileInput.value = '';
});
zone.addEventListener('dragover', (event) => {
  event.preventDefault();
  zone.classList.add('dropping');
});
zone.addEventListener('dragleave', (event) => {
  if (!zone.contains(event.relatedTarget)) zone.classList.remove('dropping');
});
zone.addEventListener('drop', (event) => {
  event.preventDefault();
  zone.classList.remove('dropping');
  openFile(event.dataTransfer.files[0]);
});
groupSearch.addEventListener('input', showGroups);
moveSearch.addEventListener('input', showMoves);
animationSearch.addEventListener('input', () => showAnimationOptions());
animationSelect.addEventListener('change', () => {
  element('apply').disabled = !animationSelect.value;
});
element('cancel').addEventListener('click', () => dialog.close());
element('remap-form').addEventListener('submit', (event) => {
  event.preventDefault();
  const replacement = Number(animationSelect.value);
  if (!cfc.animations.has(replacement)) return;
  if (replacement === selectedMove.animation) changes.delete(selectedMove.offset);
  else changes.set(selectedMove.offset, replacement);
  dialog.close();
  showMoves();
  showChanges();
  (element('command-content').querySelector(`[data-command="${selectedMove.offset}"]`) ?? moveSearch).focus();
});
outputButton.addEventListener('click', () => {
  const blob = new Blob([cfc.export(changes)], { type: 'application/octet-stream' });
  const url = URL.createObjectURL(blob);
  const link = node('a', '');
  link.href = url;
  link.download = 'fighter_command_main.cfc';
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
  status(`Downloaded fighter_command_main.cfc with ${changes.size} ${changes.size === 1 ? 'change' : 'changes'}.`);
});
showGroups();
showMoves();
