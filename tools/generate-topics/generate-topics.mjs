/**
 * generate-topics – erzeugt die Topic-Fixture `docs/topics.json` aus dem
 * Legacy-Item-Katalog (`src_old/ComfoBoxLib/Items/**`).
 *
 * Hintergrund
 * -----------
 * `docs/topics.json` ist die **Test-Fixture** des Contract-Tests. Sie wird
 * nicht von Hand gepflegt, sondern aus dem Item-Katalog erzeugt – sonst
 * laufen Katalog und MQTT-Vertrag auseinander (Befund: 2017 erzeugte Fixture
 * vs. später geänderter Legacy-Code).
 *
 * Aufruf
 * ------
 *   node tools/generate-topics/generate-topics.mjs --check
 *       Prüft, ob die committete Fixture noch zum Katalog passt.
 *       Exit 0 = deckungsgleich, Exit 1 = Abweichung.
 *
 *   node tools/generate-topics/generate-topics.mjs --out docs/topics.json
 *       Schreibt die Fixture neu.
 *
 *   node tools/generate-topics/generate-topics.mjs --catalog <datei.cs>
 *       Erzeugt den C#-Item-Katalog für `ComfoBox.Domain` (Aufgabe 2.1).
 *
 * Quellen
 * -------
 * - Item-Katalog:  src_old/ComfoBoxLib/Items/{Zone,Tests,Warmwater,States,Time,Config}.cs
 * - Reihenfolge:   src_old/ComfoBoxMqtt/Groups/ItemFactory.cs
 * - Encoding:      Windows-1252 (die Altdateien sind nicht UTF-8)
 */

import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, '..', '..');
const LEGACY_ITEMS = join(REPO, 'src_old', 'ComfoBoxLib', 'Items');

/** Reihenfolge aus ItemFactory.CreateItems – verbindlich. */
const CLASS_FILES = ['Zone', 'Tests', 'Warmwater', 'States', 'Time', 'Config'];

/** Standard-BaseTopic der Fixture. */
const BASE_TOPIC = 'ComfoBox';

/**
 * Bekannte Abweichungen zwischen Fixture (Stand 2017) und dem heutigen
 * Legacy-Katalog. Sie werden im Prüfmodus gemeldet, aber nicht als Fehler
 * gewertet – die Fixture bleibt unverändert (Projektregel: Fixtures sind
 * heilig). Sobald der neue Domain-Katalog steht, entfällt die Ausnahme.
 */
const KNOWN_DEVIATIONS = [
  {
    topic: 'ComfoBox/Warmwater/DoHeatWaterNow',
    reason: 'Fixture (2017) nennt DateTime?, der Legacy-Katalog nutzt heute AnalogValue (float?).',
  },
];

// ---------------------------------------------------------------------------
// Encoding
// ---------------------------------------------------------------------------

/**
 * Windows-1252 → JavaScript-String.
 * Die Altdateien enthalten Bytes 0x80–0x9F, die in Latin-1 andere Zeichen
 * bedeuten (z. B. 0x96 = Gedankenstrich). Ohne diese Tabelle gäbe es
 * Steuerzeichen im Text.
 */
const CP1252_HIGH = {
  0x80: '€', 0x82: '‚', 0x83: 'ƒ', 0x84: '„', 0x85: '…',
  0x86: '†', 0x87: '‡', 0x88: 'ˆ', 0x89: '‰', 0x8a: 'Š',
  0x8b: '‹', 0x8c: 'Œ', 0x8e: 'Ž', 0x91: '‘', 0x92: '’',
  0x93: '“', 0x94: '”', 0x95: '•', 0x96: '–', 0x97: '—',
  0x98: '˜', 0x99: '™', 0x9a: 'š', 0x9b: '›', 0x9c: 'œ',
  0x9e: 'ž', 0x9f: 'Ÿ',
};

function readCp1252(path) {
  const bytes = readFileSync(path);
  let out = '';
  for (const byte of bytes) {
    out += byte >= 0x80 && byte <= 0x9f ? CP1252_HIGH[byte] : String.fromCharCode(byte);
  }
  return out;
}

// ---------------------------------------------------------------------------
// Parser
// ---------------------------------------------------------------------------

/** Findet den Body-Bereich einer Klasse (Klammer-Tiefenzählung). */
function classBody(text, from) {
  const open = text.indexOf('{', from);
  if (open < 0) return null;
  let depth = 0;
  for (let i = open; i < text.length; i++) {
    const ch = text[i];
    if (ch === '{') depth++;
    else if (ch === '}') {
      depth--;
      if (depth === 0) return { start: open + 1, end: i };
    }
  }
  return null;
}

/** Zerlegt eine Argumentliste anhand der Kommas (Klammern respektiert). */
function splitArgs(text) {
  const parts = [];
  let depth = 0;
  let inString = false;
  let current = '';
  for (const ch of text) {
    if (inString) {
      current += ch;
      if (ch === '"') inString = false;
      continue;
    }
    if (ch === '"') { inString = true; current += ch; continue; }
    if (ch === '(' || ch === '[') depth++;
    if (ch === ')' || ch === ']') depth--;
    if (ch === ',' && depth === 0) { parts.push(current.trim()); current = ''; continue; }
    current += ch;
  }
  if (current.trim().length > 0) parts.push(current.trim());
  return parts;
}

/** Wandelt ein C#-Stringliteral ( @"…" / "…" ) in den Inhalt. */
function literalToString(raw) {
  const text = raw.trim();
  if (text === 'null') return null;
  const verbatim = text.startsWith('@"');
  const body = verbatim ? text.slice(2, -1) : text.slice(1, -1);
  return verbatim ? body.replace(/""/g, '"') : body.replace(/\\(.)/g, '$1');
}

/** Wandelt ein C#-Zahlenliteral in eine Zahl. */
function literalToNumber(raw) {
  const text = raw.trim();
  if (text === 'null' || text === '') return null;
  // C#-Literale: 5, -50, 0.0f, 1.5f, 3d, 2m
  const value = Number(text.replace(/[fFdDmM]$/, ''));
  return Number.isNaN(value) ? null : value;
}

// Bezeichner dürfen Umlaute enthalten (z. B. "Gebäudeträgheit").
// \w matcht in JavaScript nur ASCII - daher \p{L} mit dem u-Flag.
const IDENT = '[\\p{L}_][\\p{L}\\p{N}_]*';
const PROP_RE = new RegExp(
  `public\\s+(${IDENT}(?:<[^;=]*?>)?)\\s+(${IDENT})\\s*=>\\s*new\\s+(${IDENT})\\s*(<[^;=]*?>)?\\s*\\(([^()]*)\\)\\s*(\\{[^{}]*\\})?\\s*;`,
  'gu',
);
const DESC_RE = /\[Description\(\s*(@"(?:[^"]|"")*"|"(?:[^"\\]|\\.)*")\s*\)\s*\]/gu;

/**
 * Werttyp-Name laut Fixture.
 * `AnalogValue` → `float?`, `DateValue`/`CommandValue` → `DateTime?`,
 * `EnumValue<T>` → `ComfoBoxLib.Items.Enums.T?`.
 */
function valueTypeOf(ctor, generic) {
  if (ctor === 'AnalogValue') return 'float?';
  if (ctor === 'DateValue' || ctor === 'CommandValue') return 'DateTime?';
  if (ctor === 'EnumValue') {
    const inner = (generic ?? '').replace(/^</, '').replace(/>$/, '').trim();
    const bare = inner.replace(/\?$/, '');
    return `ComfoBoxLib.Items.Enums.${bare}?`;
  }
  return ctor;
}

/** Liest den `IsReadOnly`-Ausdruck aus einem Objektinitialisierer. */
function isReadOnlyOf(initializer) {
  if (!initializer) return false;
  const match = /IsReadOnly\s*=\s*([^;}]+)/.exec(initializer);
  if (!match) return false;
  const expr = match[1].trim();
  if (expr === 'true') return true;
  if (expr === 'false') return false;
  // `!Settings.Default.ExpertMode` – ExpertMode ist per Voreinstellung aus,
  // also ist der Wert im Auslieferungszustand `true`.
  if (expr.includes('ExpertMode')) return true;
  return false;
}

/**
 * Entfernt Kommentare, ohne die Zeilen-/Indexstruktur zu verändern
 * (Kommentarzeichen werden durch Leerzeichen ersetzt).
 *
 * Nötig, weil im Legacy-Katalog mehrere Items **auskommentiert** sind
 * (z. B. `States/Outputs/R2`) - die gehören nicht zum aktiven Katalog.
 */
function stripComments(text) {
  let out = '';
  let i = 0;
  while (i < text.length) {
    const ch = text[i];

    // Verbatim-String @"..."
    if (ch === '@' && text[i + 1] === '"') {
      out += '@"';
      i += 2;
      while (i < text.length) {
        if (text[i] === '"' && text[i + 1] === '"') { out += '""'; i += 2; continue; }
        if (text[i] === '"') { out += '"'; i++; break; }
        out += text[i];
        i++;
      }
      continue;
    }

    // Normaler String "..."
    if (ch === '"') {
      out += '"';
      i++;
      while (i < text.length) {
        if (text[i] === '\\') { out += text[i] + (text[i + 1] ?? ''); i += 2; continue; }
        if (text[i] === '"') { out += '"'; i++; break; }
        out += text[i];
        i++;
      }
      continue;
    }

    // Zeilenkommentar
    if (ch === '/' && text[i + 1] === '/') {
      while (i < text.length && text[i] !== '\n') { out += ' '; i++; }
      continue;
    }

    // Blockkommentar
    if (ch === '/' && text[i + 1] === '*') {
      while (i < text.length && !(text[i] === '*' && text[i + 1] === '/')) {
        out += text[i] === '\n' ? '\n' : ' ';
        i++;
      }
      out += '  ';
      i += 2;
      continue;
    }

    out += ch;
    i++;
  }
  return out;
}

/**
 * Zerlegt eine Klasse in Unterklassen und Properties.
 * Die Reihenfolge entspricht `ItemFactory.CreateItemsFromClass`:
 * erst die verschachtelten Klassen, dann die eigenen Properties.
 */
function parseClass(text, bodyStart, bodyEnd, path, items) {
  const body = text.slice(bodyStart, bodyEnd);

  // Verschachtelte Klassen finden
  const nested = [];
  const classRe = new RegExp(`public\\s+class\\s+(${IDENT})`, 'gu');
  let match;
  while ((match = classRe.exec(body)) !== null) {
    const range = classBody(body, match.index);
    if (!range) continue;
    nested.push({ name: match[1], start: range.start, end: range.end, index: match.index });
    classRe.lastIndex = range.end;
  }

  // Properties des eigenen Klassenrumpfs (nicht in Unterklassen)
  const ownSegment = [];
  let cursor = 0;
  for (const sub of nested) {
    ownSegment.push(body.slice(cursor, sub.index));
    cursor = sub.end;
  }
  ownSegment.push(body.slice(cursor));

  const descriptionsBySegment = ownSegment.map((segment) => {
    const found = [];
    DESC_RE.lastIndex = 0;
    let desc;
    while ((desc = DESC_RE.exec(segment)) !== null) {
      found.push({ index: desc.index, value: literalToString(desc[1]) });
    }
    return found;
  });

  const properties = [];
  for (const [segmentIndex, segment] of ownSegment.entries()) {
    const descriptions = descriptionsBySegment[segmentIndex];
    PROP_RE.lastIndex = 0;
    let prop;
    let lastPropEnd = 0;
    while ((prop = PROP_RE.exec(segment)) !== null) {
      const [, declaredType, name, ctor, generic, args, initializer] = prop;
      const argList = splitArgs(args);

      // Die Beschreibung muss zwischen der vorigen Property und dieser
      // liegen - sonst würde ein Item die Beschreibung des Vorgängers erben.
      const candidates = descriptions
        .filter((d) => d.index >= lastPropEnd && d.index < prop.index)
        .sort((a, b) => b.index - a.index);
      const description = candidates.length > 0 ? candidates[0].value : null;
      lastPropEnd = prop.index + prop[0].length;

      const unit = argList.length > 1 ? literalToString(argList[1]) : null;
      const min = argList.length > 2 ? literalToNumber(argList[2]) : null;
      const max = argList.length > 3 ? literalToNumber(argList[3]) : null;
      const readOnly = isReadOnlyOf(initializer);

      const subscribeTopic = `${[...path, name].join('/')}`;
      properties.push({
        SubscribeTopic: subscribeTopic,
        PublishTopic: readOnly ? null : `${subscribeTopic}/Set`,
        Description: description,
        ValueType: valueTypeOf(ctor, generic),
        Unit: unit,
        IsReadOnly: readOnly,
        Min: min,
        Max: max,
        DeclaredType: declaredType,
        BacnetId: argList.length > 0 ? Number(argList[0]) : null,
      });
    }
  }

  for (const sub of nested) {
    parseClass(text, bodyStart + sub.start, bodyStart + sub.end, [...path, sub.name], items);
  }
  items.push(...properties);
}

/** Liest den gesamten Katalog ein. */
export function readCatalog() {
  const items = [];
  for (const file of CLASS_FILES) {
    const path = join(LEGACY_ITEMS, `${file}.cs`);
    const text = stripComments(readCp1252(path));
    const classRe = new RegExp(`public\\s+class\\s+${file}\\b`);
    const match = classRe.exec(text);
    if (!match) throw new Error(`Klasse ${file} nicht gefunden in ${path}`);
    const range = classBody(text, match.index);
    if (!range) throw new Error(`Klassenkörper von ${file} nicht gefunden`);
    parseClass(text, range.start, range.end, [BASE_TOPIC, file], items);
  }
  return items;
}

// ---------------------------------------------------------------------------
// Serialisierung
// ---------------------------------------------------------------------------

/** Zahl wie Newtonsoft.Json: `5` → `5.0`. */
function formatNumber(value) {
  if (value === null || value === undefined) return 'null';
  return Number.isInteger(value) ? `${value}.0` : String(value);
}

function escapeJson(text) {
  return JSON.stringify(text);
}

/** Serialisiert im exakten Format der Fixture (minifiziert, kein Newline am Ende). */
export function serialize(items) {
  const parts = items.map((item) => {
    const fields = [
      `"SubscribeTopic":${escapeJson(item.SubscribeTopic)}`,
      `"PublishTopic":${item.PublishTopic === null ? 'null' : escapeJson(item.PublishTopic)}`,
      `"Description":${item.Description === null ? 'null' : escapeJson(item.Description)}`,
      `"ValueType":${escapeJson(item.ValueType)}`,
      `"Unit":${item.Unit === null ? 'null' : escapeJson(item.Unit)}`,
      `"IsReadOnly":${item.IsReadOnly ? 'true' : 'false'}`,
      `"Min":${formatNumber(item.Min)}`,
      `"Max":${formatNumber(item.Max)}`,
    ];
    return `{${fields.join(',')}}`;
  });
  return `[${parts.join(',')}]`;
}

// ---------------------------------------------------------------------------
// C#-Katalog (Aufgabe 2.1)
// ---------------------------------------------------------------------------

const CS_IDENT = (name) => name.replace(/[^A-Za-z0-9_]/g, '_');

/** Erzeugt den deklarativen C#-Item-Katalog für `ComfoBox.Domain`. */
export function emitCatalog(items) {
  const groups = new Map();
  for (const item of items) {
    const parts = item.SubscribeTopic.split('/');
    const group = parts.slice(1, -1).join('/') || parts[1];
    if (!groups.has(group)) groups.set(group, []);
    groups.get(group).push(item);
  }

  const lines = [];
  lines.push('// <auto-generated>');
  lines.push('//   Erzeugt von tools/generate-topics/generate-topics.mjs aus dem Legacy-Katalog.');
  lines.push('//   Quelle: src_old/ComfoBoxLib/Items/** – nicht von Hand bearbeiten.');
  lines.push('// </auto-generated>');
  lines.push('');
  lines.push('namespace ComfoBox.Domain.Items;');
  lines.push('');
  lines.push('/// <summary>');
  lines.push('/// Der vollständige Item-Katalog der ComfoBox Series 5.');
  lines.push('/// </summary>');
  lines.push('public static partial class Catalog');
  lines.push('{');
  lines.push('    /// <summary>');
  lines.push('    /// Alle bekannten Items, in Katalog-Reihenfolge.');
  lines.push('    /// </summary>');
  lines.push('    public static IReadOnlyList<ItemDefinition> Items { get; } =');
  lines.push('    [');
  for (const item of items) {
    const parts = item.SubscribeTopic.split('/');
    const name = parts[parts.length - 1];
    const group = parts.slice(1, -1).join('/') || parts[1];
    const unit = item.Unit === null ? 'null' : escapeCsString(item.Unit);
    const min = item.Min === null ? 'null' : formatNumber(item.Min);
    const max = item.Max === null ? 'null' : formatNumber(item.Max);
    lines.push(`        new("${name}", "${group}", ${item.BacnetId ?? 0}, ${escapeCsString(item.ValueType)}, ${unit}, ${min}, ${max}, ${item.IsReadOnly ? 'true' : 'false'}),`);
  }
  lines.push('    ];');
  lines.push('}');
  lines.push('');
  return lines.join('\n');
}

function escapeCsString(text) {
  return `"${text.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`;
}

// ---------------------------------------------------------------------------
// Vergleich
// ---------------------------------------------------------------------------

/** Vergleicht zwei Item-Listen feldweise. */
export function diff(expected, actual) {
  const differences = [];
  const byTopic = new Map(actual.map((item) => [item.SubscribeTopic, item]));
  for (const want of expected) {
    const got = byTopic.get(want.SubscribeTopic);
    if (!got) {
      differences.push({ topic: want.SubscribeTopic, field: '(neu)', expected: '(fehlt)', actual: '(fehlt)' });
      continue;
    }
    for (const field of Object.keys(want)) {
      if (field === 'DeclaredType' || field === 'BacnetId') continue;
      const a = want[field];
      const b = got[field];
      if (a === null && b === null) continue;
      if (a !== b) {
        differences.push({ topic: want.SubscribeTopic, field, expected: a, actual: b });
      }
    }
  }
  const known = new Set(KNOWN_DEVIATIONS.map((d) => d.topic));
  const relevant = differences.filter((d) => !known.has(d.topic));
  const ignored = differences.filter((d) => known.has(d.topic));

  // Items, die nur im Katalog existieren (später im Legacy ergänzt)
  const fixtureTopics = new Set(expected.map((item) => item.SubscribeTopic));
  for (const item of actual) {
    if (!fixtureTopics.has(item.SubscribeTopic) && !known.has(item.SubscribeTopic)) {
      relevant.push({
        topic: item.SubscribeTopic,
        field: '(zusätzlich)',
        expected: '(nicht in der Fixture)',
        actual: 'im Katalog vorhanden',
      });
    }
  }

  return { relevant, ignored };
}

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function main(argv) {
  const items = readCatalog();
  const json = serialize(items);

  if (argv.includes('--catalog')) {
    const index = argv.indexOf('--catalog');
    const target = argv[index + 1];
    if (!target) throw new Error('--catalog braucht einen Dateinamen');
    writeFileSync(resolve(process.cwd(), target), emitCatalog(items), 'utf8');
    process.stdout.write(`Katalog geschrieben: ${target} (${items.length} Items)\n`);
    return 0;
  }

  if (argv.includes('--check')) {
    const fixturePath = join(REPO, 'docs', 'topics.json');
    // Die Fixture hat ein UTF-8-BOM - JSON.parse mag das nicht.
    const committed = JSON.parse(readFileSync(fixturePath, 'utf8').replace(/^﻿/, ''));
    const { relevant, ignored } = diff(committed, items);

    for (const entry of ignored) {
      process.stdout.write(`~ bekannt: ${entry.topic} (${entry.field})\n`);
    }
    if (relevant.length > 0) {
      process.stdout.write(`\n${relevant.length} unerwartete Abweichung(en):\n`);
      for (const entry of relevant.slice(0, 40)) {
        process.stdout.write(`  ${entry.topic} [${entry.field}]: Fixture=${JSON.stringify(entry.expected)} Katalog=${JSON.stringify(entry.actual)}\n`);
      }
      process.stdout.write('\nFixture-Bearbeitung ist nicht erlaubt – bitte den Katalog prüfen.\n');
      return 1;
    }
    process.stdout.write(`Fixture ist deckungsgleich (${items.length} Items).\n`);
    return 0;
  }

  const index = argv.indexOf('--out');
  const target = index >= 0 ? argv[index + 1] : join(REPO, 'docs', 'topics.json');
  writeFileSync(resolve(process.cwd(), target), json, 'utf8');
  process.stdout.write(`Fixture geschrieben: ${target} (${items.length} Items)\n`);
  return 0;
}

if (process.argv[1] && resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url))) {
  process.exitCode = main(process.argv.slice(2));
}
