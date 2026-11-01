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
 *   node tools/generate-topics/generate-topics.mjs --catalog <zielordner>
 *       Erzeugt den C#-Item-Katalog für `ComfoBox.Domain` (Aufgabe 2.1):
 *       `Catalog.cs` (127 Items) und `ItemEnums.cs` (Enum-Werte).
 *
 *   node tools/generate-topics/generate-topics.mjs --catalog-check <zielordner>
 *       Prüft, ob die committeten Katalog-Dateien noch zum Legacy-Katalog passen.
 *       Exit 0 = deckungsgleich, Exit 1 = Abweichung.
 *
 * Quellen
 * -------
 * - Item-Katalog:  src_old/ComfoBoxLib/Items/{Zone,Tests,Warmwater,States,Time,Config}.cs
 * - Enums:         src_old/ComfoBoxLib/Items/Enums/*.cs
 * - Reihenfolge:   src_old/ComfoBoxMqtt/Groups/ItemFactory.cs
 * - Encoding:      Windows-1252 (die Altdateien sind nicht UTF-8)
 */

import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(HERE, '..', '..');
const LEGACY_ITEMS = join(REPO, 'src_old', 'ComfoBoxLib', 'Items');
const LEGACY_ENUMS = join(LEGACY_ITEMS, 'Enums');

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

/**
 * Liest eine Legacy-Datei mit **automatischer** Kodierungserkennung.
 *
 * Der Altbestand ist gemischt: die Item-Dateien (Zone, States, Config, …)
 * sind Windows-1252, die Enum-Dateien und `Time.cs` dagegen UTF-8. Ein
 * blindes UTF-8-Lesen erzeugt bei CP1252-Dateien Ersatzzeichen, ein
 * blindes CP1252-Lesen zerlegt Umlaute in zwei Bytes ("ä" → "Ã¤").
 *
 * @param {string} path Pfad der Datei.
 * @returns {string} Der dekodierte Inhalt.
 */
export function readLegacyFile(path) {
  const bytes = readFileSync(path);
  const asUtf8 = bytes.toString('utf8');
  // Ungültige UTF-8-Sequenzen werden beim Re-Encodieren zu U+FFFD -
  // die Bytes stimmen dann nicht mehr überein.
  if (Buffer.compare(Buffer.from(asUtf8, 'utf8'), bytes) === 0) {
    return asUtf8;
  }
  return readCp1252(path);
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
  // Zeilenenden vereinheitlichen.
  //
  // Ein verbatim-Literal über mehrere Zeilen enthält die **physischen**
  // Zeilenenden der Quelldatei. Die sind auf Windows CRLF und unter Linux
  // LF - der Generator wäre damit plattformabhängig. Die Fixture
  // (`docs/topics.json`) enthält CRLF (sie wurde unter Windows erzeugt und
  // ist eingefroren), also wird hier **immer** auf CRLF normalisiert.
  //
  // Unter Windows ist das eine No-Operation (das Literal enthält schon
  // CRLF) - die Fixture und `Catalog.cs` bleiben byteweise unverändert.
  // Unter Linux wird der Generator dadurch identisch.
  const normalized = body.replace(/\r\n|\r|\n/g, '\r\n');
  return verbatim ? normalized.replace(/""/g, '"') : normalized.replace(/\\(.)/g, '$1');
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

/** Prioritäts-Attribute: einmalig beim Start, häufig oder selten. */
const PRIORITY_RE = /\[(Once|Primary|Secondary)\]/gu;

/** Expert-Modus-Attribut. */
const EXPERT_RE = /\[ExpertMode\]/gu;

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

/**
 * Der reine Enum-Typname aus `EnumValue<T?>` (ohne Namespace, ohne `?`).
 */
function enumTypeOf(generic) {
  const inner = (generic ?? '').replace(/^</, '').replace(/>$/, '').trim();
  return inner.replace(/\?$/, '');
}

/**
 * Ersetzt den Inhalt von String-Literalen durch Leerzeichen.
 *
 * Nötig für die Enum-Analyse: die `[Description("…")]`-Attribute enthalten
 * Kommata und Wörter, die sonst als Enum-Mitglieder gelesen würden
 * (z. B. "ausgeschaltet, Energieerzeugung").
 */
function stripStrings(text) {
  let out = '';
  let i = 0;
  while (i < text.length) {
    const ch = text[i];
    if (ch === '@' && text[i + 1] === '"') {
      out += '  ';
      i += 2;
      while (i < text.length) {
        if (text[i] === '"' && text[i + 1] === '"') { out += '  '; i += 2; continue; }
        if (text[i] === '"') { out += ' '; i++; break; }
        out += text[i] === '\n' ? '\n' : ' ';
        i++;
      }
      continue;
    }
    if (ch === '"') {
      out += ' ';
      i++;
      while (i < text.length) {
        if (text[i] === '\\') { out += '  '; i += 2; continue; }
        if (text[i] === '"') { out += ' '; i++; break; }
        out += text[i] === '\n' ? '\n' : ' ';
        i++;
      }
      continue;
    }
    out += ch;
    i++;
  }
  return out;
}

/** Findet den Body-Bereich eines Enums (Klammer-Tiefenzählung). */
function enumBody(text, from) {
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

/**
 * Liest die Mitglieder eines Enum-Bodies.
 * Werte ohne `=` folgen dem vorherigen Member (+1).
 */
function parseEnumMembers(body) {
  const members = [];
  let next = 0;
  const re = /([\p{L}_][\p{L}\p{N}_]*)\s*(?:=\s*(-?\d+))?\s*(?:,|$)/gu;
  let match;
  while ((match = re.exec(body)) !== null) {
    const value = match[2] !== undefined ? Number(match[2]) : next;
    members.push({ name: match[1], value });
    next = value + 1;
  }
  return members;
}

/**
 * Liest alle Enums aus `src_old/ComfoBoxLib/Items/Enums/*.cs`.
 *
 * @returns {Map<string, Array<{name: string, value: number}>>}
 */
export function readEnums() {
  const result = new Map();
  const files = readdirSync(LEGACY_ENUMS).filter((f) => f.endsWith('.cs')).sort();
  for (const file of files) {
    const text = stripComments(readLegacyFile(join(LEGACY_ENUMS, file)));
    const enumRe = /public\s+enum\s+([\p{L}_][\p{L}\p{N}_]*)/gu;
    let match;
    while ((match = enumRe.exec(text)) !== null) {
      const name = match[1];
      const body = enumBody(text, match.index);
      if (!body) continue;
      result.set(name, parseEnumMembers(stripStrings(text.slice(body.start, body.end))));
      enumRe.lastIndex = body.end;
    }
  }
  return result;
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
 * `true`, wenn `IsReadOnly` fest gesetzt ist (nicht vom Expert-Modus abhängt).
 *
 * Für den Domain-Katalog wird der Expert-Modus getrennt geführt
 * (`RequiresExpertMode`) - so bleibt der effektive Zustand berechenbar.
 */
function isReadOnlyLiteral(initializer) {
  if (!initializer) return false;
  const match = /IsReadOnly\s*=\s*([^;}]+)/.exec(initializer);
  if (!match) return false;
  return match[1].trim() === 'true';
}

/**
 * `true`, wenn `IsReadOnly` vom Expert-Modus abhängt.
 */
function dependsOnExpertMode(initializer) {
  if (!initializer) return false;
  const match = /IsReadOnly\s*=\s*([^;}]+)/.exec(initializer);
  if (!match) return false;
  return match[1].includes('ExpertMode');
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

  // Prioritäts- und Expert-Modus-Attribute je Segment einsammeln.
  const prioritiesBySegment = ownSegment.map((segment) => {
    const found = [];
    PRIORITY_RE.lastIndex = 0;
    let prio;
    while ((prio = PRIORITY_RE.exec(segment)) !== null) {
      found.push({ index: prio.index, value: prio[1] });
    }
    return found;
  });

  const expertsBySegment = ownSegment.map((segment) => {
    const found = [];
    EXPERT_RE.lastIndex = 0;
    let expert;
    while ((expert = EXPERT_RE.exec(segment)) !== null) {
      found.push({ index: expert.index });
    }
    return found;
  });

  const properties = [];
  for (const [segmentIndex, segment] of ownSegment.entries()) {
    const descriptions = descriptionsBySegment[segmentIndex];
    const priorities = prioritiesBySegment[segmentIndex];
    const experts = expertsBySegment[segmentIndex];
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

      // Dasselbe gilt für die Priorität: das Attribut steht unmittelbar
      // vor der Property (Befund A15 - im Legacy wurde es verworfen).
      const priorityCandidates = priorities
        .filter((p) => p.index >= lastPropEnd && p.index < prop.index)
        .sort((a, b) => b.index - a.index);
      const priority = priorityCandidates.length > 0 ? priorityCandidates[0].value : 'None';

      const expertCandidates = experts.filter((e) => e.index >= lastPropEnd && e.index < prop.index);
      lastPropEnd = prop.index + prop[0].length;

      const unit = argList.length > 1 ? literalToString(argList[1]) : null;
      const min = argList.length > 2 ? literalToNumber(argList[2]) : null;
      const max = argList.length > 3 ? literalToNumber(argList[3]) : null;
      const readOnly = isReadOnlyOf(initializer);
      const requiresExpertMode = expertCandidates.length > 0 || dependsOnExpertMode(initializer);

      const subscribeTopic = `${[...path, name].join('/')}`;
      properties.push({
        SubscribeTopic: subscribeTopic,
        PublishTopic: readOnly ? null : `${subscribeTopic}/Set`,
        Description: description,
        ValueType: valueTypeOf(ctor, generic),
        Unit: unit,
        IsReadOnly: readOnly,
        RequiresExpertMode: requiresExpertMode,
        IsReadOnlyLiteral: isReadOnlyLiteral(initializer),
        Priority: priority,
        Ctor: ctor,
        EnumType: ctor === 'EnumValue' ? enumTypeOf(generic) : null,
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
    const text = stripComments(readLegacyFile(path));
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

/** camelCase → snake_case (für die REST-Id eines Items). */
function toSnake(name) {
  return name
    .replace(/([A-Z]+)([A-Z][a-z])/g, '$1_$2')
    .replace(/([a-z0-9])([A-Z])/g, '$1_$2')
    .toLowerCase();
}

/** Die stabile Id eines Items: Punkt- getrennter Pfad in snake_case. */
function toId(group, name) {
  return [...group.split('/'), name].map(toSnake).join('.');
}

/** Der Werte-Kind des Legacy-Werttyps. */
function kindOf(item) {
  switch (item.Ctor) {
    case 'AnalogValue':
      return 'Analog';
    case 'EnumValue':
      return 'Enum';
    case 'DateValue':
      return 'Date';
    default:
      throw new Error(`Unbekannter Werttyp ${item.Ctor} in ${item.SubscribeTopic}`);
  }
}

/** Zahl als C#-float-Literal. */
function formatCsFloat(value) {
  if (value === null || value === undefined) return 'null';
  const text = Number.isInteger(value) ? `${value}.0` : String(value);
  return `${text}f`;
}

/**
 * Erzeugt den deklarativen C#-Item-Katalog für `ComfoBox.Domain`.
 *
 * @param {Array} items Der gelesene Legacy-Katalog.
 * @returns {string} Der Inhalt von `Catalog.cs`.
 */
export function emitCatalog(items) {
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
  lines.push('/// <remarks>');
  lines.push('/// Die Reihenfolge entspricht <c>ItemFactory.CreateItems</c> des Legacy-Codes');
  lines.push('/// und damit der Reihenfolge der MQTT-Topics.');
  lines.push('/// </remarks>');
  lines.push('public static partial class Catalog');
  lines.push('{');
  lines.push('    /// <summary>');
  lines.push('    /// Alle bekannten Items in Katalog-Reihenfolge.');
  lines.push('    /// </summary>');
  lines.push('    public static IReadOnlyList<ItemDefinition> All { get; } =');
  lines.push('    [');

  for (const item of items) {
    const parts = item.SubscribeTopic.split('/');
    const name = parts[parts.length - 1];
    const group = parts.slice(1, -1).join('/') || parts[1];
    const kind = kindOf(item);

    lines.push('        new()');
    lines.push('        {');
    lines.push(`            Id = ${escapeCsString(toId(group, name))},`);
    lines.push(`            Group = ${escapeCsString(group)},`);
    lines.push(`            Name = ${escapeCsString(name)},`);
    lines.push(`            BacnetObjectId = ${item.BacnetId ?? 0},`);
    lines.push(`            Kind = ItemValueKind.${kind},`);
    if (item.Unit !== null) lines.push(`            Unit = ${escapeCsString(item.Unit)},`);
    if (item.Min !== null) lines.push(`            Min = ${formatCsFloat(item.Min)},`);
    if (item.Max !== null) lines.push(`            Max = ${formatCsFloat(item.Max)},`);
    if (item.IsReadOnlyLiteral) lines.push('            IsReadOnly = true,');
    if (item.RequiresExpertMode) lines.push('            RequiresExpertMode = true,');
    lines.push(`            Priority = RefreshPriority.${item.Priority},`);
    if (item.Description !== null) lines.push(`            Description = ${escapeCsString(item.Description)},`);
    if (kind === 'Enum') lines.push(`            EnumValues = ItemEnums.${item.EnumType},`);
    lines.push('        },');
  }

  lines.push('    ];');
  lines.push('}');
  lines.push('');
  return lines.join('\n');
}

/**
 * Erzeugt die Enum-Werte für `ComfoBox.Domain`.
 *
 * @param {Map<string, Array<{name: string, value: number}>>} enums Die gelesenen Enums.
 * @returns {string} Der Inhalt von `ItemEnums.cs`.
 */
export function emitEnums(enums) {
  const lines = [];
  lines.push('// <auto-generated>');
  lines.push('//   Erzeugt von tools/generate-topics/generate-topics.mjs aus den Legacy-Enums.');
  lines.push('//   Quelle: src_old/ComfoBoxLib/Items/Enums/*.cs – nicht von Hand bearbeiten.');
  lines.push('// </auto-generated>');
  lines.push('');
  lines.push('namespace ComfoBox.Domain.Items;');
  lines.push('');
  lines.push('/// <summary>');
  lines.push('/// Die Enum-Werte der ComfoBox.');
  lines.push('/// </summary>');
  lines.push('/// <remarks>');
  lines.push('/// Die Namen sind Teil des MQTT-Vertrags und dürfen nicht geändert werden.');
  lines.push('/// </remarks>');
  lines.push('public static class ItemEnums');
  lines.push('{');

  for (const [name, members] of enums) {
    lines.push('    /// <summary>');
    lines.push(`    /// Die Werte des Legacy-Enums <c>${name}</c>.`);
    lines.push('    /// </summary>');
    lines.push(`    public static IReadOnlyDictionary<int, string> ${name} { get; } = new Dictionary<int, string>`);
    lines.push('    {');
    for (const member of members) {
      lines.push(`        [${member.value}] = ${escapeCsString(member.name)},`);
    }
    lines.push('    };');
    lines.push('');
  }

  lines.push('}');
  lines.push('');
  return lines.join('\n');
}

function escapeCsString(text) {
  return `"${String(text)
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"')
    .replace(/\r/g, '\\r')
    .replace(/\n/g, '\\n')
    .replace(/\t/g, '\\t')}"`;
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

  if (argv.includes('--catalog') || argv.includes('--catalog-check')) {
    const check = argv.includes('--catalog-check');
    const flag = check ? '--catalog-check' : '--catalog';
    const index = argv.indexOf(flag);
    const target = argv[index + 1];
    if (!target) throw new Error(`${flag} braucht einen Zielordner`);

    const directory = resolve(process.cwd(), target);
    const files = {
      'Catalog.cs': emitCatalog(items),
      'ItemEnums.cs': emitEnums(readEnums()),
    };

    if (check) {
      const differences = [];
      for (const [name, content] of Object.entries(files)) {
        const path = join(directory, name);
        if (!existsSync(path)) {
          differences.push(`${name}: fehlt`);
          continue;
        }
        const committed = readFileSync(path, 'utf8').replace(/^﻿/, '');
        if (committed !== content) differences.push(`${name}: weicht ab`);
      }
      if (differences.length > 0) {
        process.stdout.write(`\n${differences.length} Abweichung(en):\n`);
        for (const entry of differences) process.stdout.write(`  ${entry}\n`);
        process.stdout.write('\nKatalog neu erzeugen: node tools/generate-topics/generate-topics.mjs --catalog <zielordner>\n');
        return 1;
      }
      process.stdout.write(`Katalog ist deckungsgleich (${items.length} Items).\n`);
      return 0;
    }

    mkdirSync(directory, { recursive: true });
    for (const [name, content] of Object.entries(files)) {
      writeFileSync(join(directory, name), content, 'utf8');
    }
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
