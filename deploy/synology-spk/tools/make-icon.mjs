#!/usr/bin/env node
// ---------------------------------------------------------------------------
// make-icon.mjs – erzeugt die Icons des Synology-Pakets (PNG).
//
// Warum ein Skript statt einer fertigen Datei?
//   Das Icon ist ein **Artefakt**, kein Quelltext. Trotzdem soll es
//   reproduzierbar sein: wer das Design ändern will, ruft das Skript auf und
//   bekommt byte-gleiche Ergebnisse.
//
// Das **Design** liegt seit 2026-11-14 in `tools/icons/comfobox-icon.mjs` –
// dieselbe Quelle wie für das Favicon und das Markenzeichen der Web-UI. Hier
// steht nur noch die Kommandozeile und der Ablageort.
//
// Aufruf:
//   node deploy/synology-spk/tools/make-icon.mjs [ziel.png]
//   node deploy/synology-spk/tools/make-icon.mjs --size 64 [ziel.png]
//
// **Beide Größen sind Pflicht** (DSM 7.0 verlangt `PACKAGE_ICON.PNG` mit
// **64×64**, die Großvariante kommt daneben):
//   node deploy/synology-spk/tools/make-icon.mjs                # 256 px
//   node deploy/synology-spk/tools/make-icon.mjs --size 64 \
//        deploy/synology-spk/src/PACKAGE_ICON_64.png
//
// Voraussetzung: nur Node.js – keine nativen Abhängigkeiten, kein Python,
// kein ImageMagick.
// ---------------------------------------------------------------------------

import { writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import { encodePng, renderIcon } from '../../../tools/icons/comfobox-icon.mjs';

// --- Argumente ---------------------------------------------------------------
const args = process.argv.slice(2);
let size = 256;
let targetArg = null;

for (let i = 0; i < args.length; i += 1) {
  if (args[i] === '--size') {
    size = Number.parseInt(args[i + 1], 10);
    i += 1;
  } else {
    targetArg = args[i];
  }
}

if (!Number.isInteger(size) || size < 16 || size > 1024) {
  console.error(`FEHLER: Ungueltige Groesse: ${size} (erlaubt: 16..1024)`);
  process.exit(1);
}

const here = dirname(fileURLToPath(import.meta.url));
const target = resolve(targetArg ?? `${here}/../src/PACKAGE_ICON.png`);

writeFileSync(target, encodePng(renderIcon({ size })));

console.log(`Icon geschrieben: ${target} (${size}×${size})`);
