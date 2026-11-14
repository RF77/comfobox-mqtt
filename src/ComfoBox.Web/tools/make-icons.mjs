#!/usr/bin/env node
// ---------------------------------------------------------------------------
// make-icons.mjs – erzeugt die Icons der Web-Oberfläche.
//
// Erzeugt werden **nur Dateien, die nicht von Hand gepflegt werden können**:
//
//   public/logo.svg               Favicon (SVG) und Markenzeichen in der Kopfzeile
//   public/favicon.ico            16/32/48 px für alte Browser und Lesezeichen
//   public/apple-touch-icon.png   180 px, randlos (iOS rundet selbst)
//   public/icon-192.png           192 px, abgerundet – Manifest (`any`)
//   public/icon-512.png           512 px, abgerundet – Manifest (`any`)
//   public/icon-maskable-512.png  512 px, randlos – Manifest (`maskable`)
//
// Die Geometrie liegt in `tools/icons/comfobox-icon.mjs` – dort ändern, dann
// dieses Skript aufrufen. Die Ergebnisse werden **versioniert**, damit ein
// Build ohne Node-Lauf reproduzierbar bleibt.
//
// Aufruf:
//   node src/ComfoBox.Web/tools/make-icons.mjs [--out <ordner>]
// ---------------------------------------------------------------------------

import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

import {
  DEFAULT_CORNER,
  encodeIco,
  encodePng,
  iconSvg,
  renderIcon,
} from '../../../tools/icons/comfobox-icon.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);

let outDir = resolve(here, '..', 'public');

for (let i = 0; i < args.length; i += 1) {
  if (args[i] === '--out') {
    outDir = resolve(args[i + 1]);
    i += 1;
  } else {
    console.error(`FEHLER: Unbekanntes Argument: ${args[i]}`);
    process.exit(2);
  }
}

mkdirSync(outDir, { recursive: true });

/**
 * Schreibt eine Datei und meldet sie auf der Konsole.
 *
 * @param {string} name Dateiname im Zielordner
 * @param {Buffer | string} content der Inhalt
 * @returns {void}
 */
function write(name, content) {
  const target = resolve(outDir, name);
  writeFileSync(target, content);
  const bytes = typeof content === 'string' ? Buffer.byteLength(content, 'utf8') : content.length;
  console.log(`  ${name.padEnd(24)} ${String(bytes).padStart(7)} Bytes`);
}

console.log(`Icons → ${outDir}`);

// Vektor: Favicon und Markenzeichen in der Kopfzeile.
write('logo.svg', iconSvg());

// Raster in der kompakten Form – ICO trägt die kleinen Auflösungen.
const icoSizes = [16, 32, 48];
write('favicon.ico', encodeIco(icoSizes.map((size) => renderIcon({ size }))));

// Randlos: iOS und Android beschneiden die Fläche selbst.
write('apple-touch-icon.png', encodePng(renderIcon({ size: 180, margin: 0, corner: 0 })));
write('icon-maskable-512.png', encodePng(renderIcon({ size: 512, margin: 0, corner: 0 })));

// Abgerundet: klassische App-Icons (Manifest `any`).
write('icon-192.png', encodePng(renderIcon({ size: 192, corner: DEFAULT_CORNER })));
write('icon-512.png', encodePng(renderIcon({ size: 512, corner: DEFAULT_CORNER })));

console.log('Fertig.');
