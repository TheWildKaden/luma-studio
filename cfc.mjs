const header = [0x43, 0x46, 0x43, 0x49, 2, 1, 0, 0];
const stringsStart = 16;
const moveSize = 20;
const groupSize = 24;

export function readCfc(input) {
  const bytes = new Uint8Array(input).slice();
  const view = new DataView(bytes.buffer);
  const u32 = (offset) => view.getUint32(offset, false);
  const require = (condition, detail) => {
    if (!condition) throw new Error(`Unsupported or damaged CFC: ${detail}.`);
  };

  require(bytes.length >= 40 && header.every((byte, i) => bytes[i] === byte), 'expected CFCI version 2.1');
  const footer = bytes.length - 8;
  const count = u32(footer);
  const table = u32(footer + 4);
  require(count > 0 && table >= stringsStart + groupSize && !(table % 4) &&
    table + count * 4 === footer, 'invalid group index');

  let recordsStart = table;
  let previousEnd = 0;
  const groups = [];
  for (let i = 0; i < count; i++) {
    const offset = u32(table + i * 4);
    require(offset >= Math.max(stringsStart, previousEnd) && !(offset % 4) &&
      offset + groupSize <= table, 'invalid group record');
    const list = u32(offset + 12);
    const length = u32(offset + 8);
    require(list >= Math.max(stringsStart, previousEnd) && !(list % 4) &&
      list + 4 * length <= offset, 'invalid command list');
    const commands = [];
    let last = previousEnd;
    for (let j = 0; j < length; j++) {
      const command = u32(list + j * 4);
      require(command >= Math.max(stringsStart, last) && !(command % 4) &&
        command + moveSize <= list, 'invalid command record');
      recordsStart = Math.min(recordsStart, command, u32(command + 12));
      commands.push(command);
      last = command + moveSize;
    }
    recordsStart = Math.min(recordsStart, list);
    groups.push({ offset, commands });
    previousEnd = offset + groupSize;
  }

  // The first two bytes of the initial label precede its referenced suffix.
  // The remaining labels are NUL-terminated and followed by 0xCC alignment.
  require(recordsStart > stringsStart && recordsStart <= table && !(recordsStart % 4), 'invalid string table');
  const strings = new Map();
  const decoder = new TextDecoder();
  let next = stringsStart;
  while (next < recordsStart && bytes[next] !== 0xcc) {
    const end = bytes.indexOf(0, next);
    require(end >= next && end < recordsStart, 'unterminated string');
    strings.set(next, decoder.decode(bytes.subarray(next, end)));
    next = end + 1;
  }
  require(recordsStart - next <= 3 && bytes.subarray(next, recordsStart).every((byte) => byte === 0xcc),
    'invalid string table alignment');

  const editable = new Map();
  const animations = new Map();
  const movesets = groups.map(({ offset, commands }) => {
    const name = strings.get(u32(offset));
    const path = strings.get(u32(offset + 4));
    require(name !== undefined && path !== undefined, 'invalid group label');
    const moves = commands.flatMap((command) => {
      const type = u32(command + 4) & 0xffff;
      if (type !== 1) return [];
      const animation = u32(command + 8);
      require(strings.has(animation), 'invalid animation reference');
      const label = strings.get(u32(command));
      if (!strings.get(animation)) return [];
      editable.set(command, animation);
      animations.set(animation, strings.get(animation));
      return [{ offset: command, name: label || 'Unnamed command', animation }];
    });
    return { offset, name, path, moves, commandCount: commands.length };
  });

  return {
    byteLength: bytes.length,
    movesets,
    animations,
    export(changes) {
      const output = bytes.slice();
      const writer = new DataView(output.buffer);
      for (const [command, animation] of changes) {
        if (!editable.has(command) || !animations.has(animation)) {
          throw new Error('The selected command or animation is outside this CFC.');
        }
        writer.setUint32(command + 8, animation, false);
      }
      return output;
    },
  };
}
