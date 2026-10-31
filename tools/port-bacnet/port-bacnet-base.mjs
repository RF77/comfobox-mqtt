// Port-Skript: BACnetBase.cs (Legacy, MIT) -> src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs
// Entfernt die UDP/BACnet-IP- und PTP-Teile (Phase 1, Aufgabe 1.6) und zieht
// den Namespace auf ComfoBox.Bacnet um.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';

const CP1252_HIGH = {
  0x80: '€', 0x82: '‚', 0x83: 'ƒ', 0x84: '„', 0x85: '…',
  0x86: '†', 0x87: '‡', 0x88: 'ˆ', 0x89: '‰', 0x8a: 'Š',
  0x8b: '‹', 0x8c: 'Œ', 0x8e: 'Ž', 0x91: '‘', 0x92: '’',
  0x93: '“', 0x94: '”', 0x95: '•', 0x96: '–', 0x97: '—',
  0x98: '˜', 0x99: '™', 0x9a: 'š', 0x9b: '›', 0x9c: 'œ',
  0x9e: 'ž', 0x9f: 'Ÿ',
};

function readAny(path) {
  const bytes = readFileSync(path);
  let text = '';
  let valid = true;
  try {
    new TextDecoder('utf-8', { fatal: true }).decode(bytes);
  } catch {
    valid = false;
  }
  if (valid) return new TextDecoder('utf-8').decode(bytes);
  for (const byte of bytes) {
    text += byte >= 0x80 && byte <= 0x9f ? CP1252_HIGH[byte] : String.fromCharCode(byte);
  }
  return text;
}

/** Entfernt einen Klassenblock anhand der Klammer-Tiefe. */
function removeClass(text, className) {
  const re = new RegExp(`^([ \\t]*)public\\s+class\\s+${className}\\b`, 'm');
  const match = re.exec(text);
  if (!match) throw new Error(`Klasse ${className} nicht gefunden`);
  const open = text.indexOf('{', match.index);
  let depth = 0;
  for (let i = open; i < text.length; i++) {
    if (text[i] === '{') depth++;
    else if (text[i] === '}') {
      depth--;
      if (depth === 0) {
        let end = i + 1;
        // nachfolgende Leerzeilen einsammeln
        while (text[end] === '\r' || text[end] === '\n') end++;
        return { text: text.slice(0, match.index) + text.slice(end), removed: match.index };
      }
    }
  }
  throw new Error(`Klasse ${className} nicht geschlossen`);
}

const source = process.argv[2];
const target = process.argv[3];

let text = readAny(source);

// 1. BACnet/IP (BVLC) und PTP entfernen - der Stack läuft nur über MS/TP.
text = removeClass(text, 'BVLC').text;
text = removeClass(text, 'PTP').text;

// 2. Namespace umziehen
text = text.replace(/namespace\s+System\.IO\.BACnet\.Serialize/g, 'namespace ComfoBox.Bacnet.Serialize');
text = text.replace(/namespace\s+System\.IO\.BACnet/g, 'namespace ComfoBox.Bacnet');
text = text.replace(/using\s+System\.IO\.BACnet\.Serialize;/g, 'using ComfoBox.Bacnet.Serialize;');
text = text.replace(/System\.IO\.BACnet\.Serialize\./g, 'ComfoBox.Bacnet.Serialize.');

// 2a. Trace -> ILogger (Befund A16). Der Codec ist statisch, deshalb laeuft die
//     Meldung ueber eine Fassade, die der Gateway beim Start belegt.
text = text.replace(
  /System\.Diagnostics\.Trace\.TraceWarning\((.*?)\);/g,
  'BacnetLog.Logger.LogWarning($1);',
);
text = text.replace(
  /using\s+System\.Diagnostics;/g,
  'using ComfoBox.Bacnet.Diagnostics;',
);

// 3. Verweise auf den entfernten UDP-Transport entfernen
text = text.replace(/^\s*BacnetIpUdpProtocolTransport\s+MyBBMDTransport;\s*$/m, '');
text = text.replace(/^\s*bool\s+BBMD_FD_ServiceActivated\s*=\s*false;\s*$/m, '');

// 3a. Der Fremdcode wird 1:1 uebernommen - die Nullable-Analyse wird fuer
//     diese Datei abgeschaltet, damit die Portierung das Verhalten aendert
//     und nicht die Nullability.
text = text.replace(/^using\s+System;/m, '#nullable disable\n\nusing System;');

mkdirSync(target.replace(/[^/\\]+$/, ''), { recursive: true });
writeFileSync(target, text, 'utf8');
console.log(`Portiert: ${source} -> ${target} (${text.split('\n').length} Zeilen)`);
