#!/usr/bin/env node
// ---------------------------------------------------------------------------
// make-icon.mjs – erzeugt das Paket-Icon des Synology-Pakets (PNG).
//
// Warum ein Skript statt einer fertigen Datei?
//   Das Icon ist ein **Artefakt**, kein Quelltext. Trotzdem soll es
//   reproduzierbar sein: wer das Design ändern will, ruft dieses Skript auf
//   und bekommt byte-gleiche Ergebnisse.
//
// Aufruf:
//   node deploy/synology-spk/tools/make-icon.mjs [ziel.png]
//   node deploy/synology-spk/tools/make-icon.mjs --size 64 [ziel.png]
//
// **Warum zwei Größen?** DSM 7.0 verlangt `PACKAGE_ICON.PNG` mit **64×64**
// (Breaking Change, vorher 72×72). Die große Variante (256×256) legt das
// Paketzentrum zusätzlich als `PACKAGE_ICON_256.PNG` ab.
//
// Voraussetzung: nur Node.js (zlib ist im Standardmodul enthalten) – keine
// nativen Abhängigkeiten, kein Python, kein ImageMagick.
// ---------------------------------------------------------------------------

import { deflateSync } from 'node:zlib';
import { writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

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

// Das Design wird immer in dieser Auflösung gezeichnet - die Geometrie
// (Ringe, Radien) ist darauf abgestimmt. Kleinere Groessen entstehen durch
// Skalieren des fertigen Rasters (Box-Filter, siehe scalePixels()).
const SIZE = 256;
const SUPERSAMPLE = 4; // 4×4 Kanten für saubere Antialiasing-Kanten

// --- Farben (RGBA) ---------------------------------------------------------
const TOP = [14, 165, 233, 255]; // sky-500
const BOTTOM = [29, 78, 216, 255]; // blue-700
const GLYPH = [255, 255, 255, 255]; // weiß
const ACCENT = [251, 191, 36, 255]; // amber-400 (der "Punkt" im C)

// --- Geometrie des Icons ----------------------------------------------------
const MARGIN = 10; // Außenrand des abgerundeten Quadrats
const CORNER = 54; // Radius der Ecken
const RING_OUTER = 74; // Außenradius des C
const RING_INNER = 47; // Innenradius des C
const GAP_START = -42; // Öffnung des C (Grad)
const GAP_END = 42;
const DOT_RADIUS = 15; // Radius des Punktes in der Öffnung
const DOT_CENTER = [SIZE / 2 + (RING_OUTER + RING_INNER) / 2, SIZE / 2];

/**
 * Prüft, ob eine Koordinate in einem abgerundeten Quadrat liegt.
 *
 * @param {number} x X-Koordinate
 * @param {number} y Y-Koordinate
 * @returns {boolean} true, wenn der Punkt innerhalb liegt
 */
function insideRoundedSquare(x, y) {
  const min = MARGIN;
  const max = SIZE - MARGIN;

  if (x < min || x > max || y < min || y > max) {
    return false;
  }

  // Der Mittelpunkt des Rechtecks.
  const cx = (min + max) / 2;
  const cy = (min + max) / 2;

  // Abstand zum Mittelpunkt in jeder Achse.
  const dx = Math.max(min + CORNER - x, 0, x - (max - CORNER));
  const dy = Math.max(min + CORNER - y, 0, y - (max - CORNER));

  return dx * dx + dy * dy <= CORNER * CORNER;
}

/**
 * Prüft, ob eine Koordinate auf dem C-Ring liegt.
 *
 * @param {number} x X-Koordinate
 * @param {number} y Y-Koordinate
 * @returns {boolean} true, wenn der Punkt auf dem Ring liegt
 */
function insideRing(x, y) {
  const cx = SIZE / 2;
  const cy = SIZE / 2;
  const dx = x - cx;
  const dy = y - cy;
  const distance = Math.hypot(dx, dy);

  if (distance < RING_INNER || distance > RING_OUTER) {
    return false;
  }

  // Die Öffnung des C liegt rechts (0°).
  let angle = (Math.atan2(dy, dx) * 180) / Math.PI;

  if (angle > GAP_END || angle < GAP_START) {
    return true;
  }

  // Der Winkel liegt in der Öffnung: nur der Punkt zählt.
  const dotDistance = Math.hypot(x - DOT_CENTER[0], y - DOT_CENTER[1]);
  return dotDistance <= DOT_RADIUS;
}

/**
 * Mischt zwei Farben.
 *
 * @param {number[]} a Farbe A (RGBA)
 * @param {number[]} b Farbe B (RGBA)
 * @param {number} t Anteil von B (0…1)
 * @returns {number[]} die gemischte Farbe
 */
function mix(a, b, t) {
  return [
    Math.round(a[0] + (b[0] - a[0]) * t),
    Math.round(a[1] + (b[1] - a[1]) * t),
    Math.round(a[2] + (b[2] - a[2]) * t),
    255,
  ];
}

/**
 * Berechnet die Farbe eines Pixels (ohne Antialiasing).
 *
 * @param {number} x X-Koordinate
 * @param {number} y Y-Koordinate
 * @returns {number[]} RGBA
 */
function pixelAt(x, y) {
  if (!insideRoundedSquare(x, y)) {
    return [0, 0, 0, 0];
  }

  if (insideRing(x, y)) {
    return GLYPH;
  }

  // Vertikaler Verlauf von oben nach unten.
  const t = (y - MARGIN) / (SIZE - 2 * MARGIN);
  return mix(TOP, BOTTOM, Math.min(1, Math.max(0, t)));
}

// --- Rasterung --------------------------------------------------------------
const pixels = Buffer.alloc(SIZE * SIZE * 4);
const step = 1 / SUPERSAMPLE;

for (let py = 0; py < SIZE; py++) {
  for (let px = 0; px < SIZE; px++) {
    let r = 0;
    let g = 0;
    let b = 0;
    let a = 0;

    for (let sy = 0; sy < SUPERSAMPLE; sy++) {
      for (let sx = 0; sx < SUPERSAMPLE; sx++) {
        const x = px + (sx + 0.5) * step;
        const y = py + (sy + 0.5) * step;
        const color = pixelAt(x, y);

        // Vormultipliziert mischen (die Farbe wird nur dort gewichtet, wo
        // überhaupt etwas gezeichnet wird).
        const alpha = color[3] / 255;
        r += color[0] * alpha;
        g += color[1] * alpha;
        b += color[2] * alpha;
        a += color[3];
      }
    }

    const samples = SUPERSAMPLE * SUPERSAMPLE;
    const alpha = a / samples;
    const offset = (py * SIZE + px) * 4;

    if (alpha === 0) {
      pixels[offset + 3] = 0;
      continue;
    }

    pixels[offset] = Math.round(r / (alpha / 255));
    pixels[offset + 1] = Math.round(g / (alpha / 255));
    pixels[offset + 2] = Math.round(b / (alpha / 255));
    pixels[offset + 3] = Math.round(alpha);
  }
}

// Das fertige 256er-Raster auf die Zielgroesse bringen (Box-Filter).
const scaled = scalePixels(pixels, SIZE, size);

// --- Skalieren ---------------------------------------------------------------

/**
 * Skaliert ein RGBA-Raster mit einem Box-Filter (Durchschnitt über das
 * Quellrechteck). Das ist der richtige Filter für Verkleinerungen: er
 * bekommt keine hellen Ringe an den Kanten – anders als Nearest Neighbor.
 *
 * @param {Buffer} src RGBA-Raster in srcSize × srcSize
 * @param {number} srcSize Kantenlänge der Quelle
 * @param {number} dstSize Kantenlänge des Ziels
 * @returns {Buffer} RGBA-Raster in dstSize × dstSize
 */
function scalePixels(src, srcSize, dstSize) {
  if (srcSize === dstSize) {
    return src;
  }

  const out = Buffer.alloc(dstSize * dstSize * 4);
  const ratio = srcSize / dstSize;

  for (let y = 0; y < dstSize; y += 1) {
    const y0 = Math.floor(y * ratio);
    const y1 = Math.max(y0 + 1, Math.floor((y + 1) * ratio));

    for (let x = 0; x < dstSize; x += 1) {
      const x0 = Math.floor(x * ratio);
      const x1 = Math.max(x0 + 1, Math.floor((x + 1) * ratio));

      let r = 0;
      let g = 0;
      let b = 0;
      let a = 0;
      let n = 0;

      for (let sy = y0; sy < y1; sy += 1) {
        for (let sx = x0; sx < x1; sx += 1) {
          const offset = (sy * srcSize + sx) * 4;
          r += src[offset];
          g += src[offset + 1];
          b += src[offset + 2];
          a += src[offset + 3];
          n += 1;
        }
      }

      const offset = (y * dstSize + x) * 4;
      out[offset] = Math.round(r / n);
      out[offset + 1] = Math.round(g / n);
      out[offset + 2] = Math.round(b / n);
      out[offset + 3] = Math.round(a / n);
    }
  }

  return out;
}

// --- PNG kodieren -----------------------------------------------------------

/**
 * Berechnet die CRC-32-Prüfsumme (PNG-Variante).
 *
 * @param {Buffer} buffer die Daten
 * @returns {number} die Prüfsumme
 */
function crc32(buffer) {
  let crc = 0xffffffff;

  for (const byte of buffer) {
    crc ^= byte;

    for (let bit = 0; bit < 8; bit++) {
      crc = crc & 1 ? (crc >>> 1) ^ 0xedb88320 : crc >>> 1;
    }
  }

  return (crc ^ 0xffffffff) >>> 0;
}

/**
 * Baut einen PNG-Chunk.
 *
 * @param {string} type der Chunk-Typ
 * @param {Buffer} data die Nutzdaten
 * @returns {Buffer} der fertige Chunk
 */
function chunk(type, data) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length, 0);

  const typeBuffer = Buffer.from(type, 'ascii');
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([typeBuffer, data])), 0);

  return Buffer.concat([length, typeBuffer, data, crc]);
}

const ihdr = Buffer.alloc(13);
ihdr.writeUInt32BE(size, 0);
ihdr.writeUInt32BE(size, 4);
ihdr[8] = 8; // Bittiefe
ihdr[9] = 6; // Farbtyp RGBA
ihdr[10] = 0; // Kompression
ihdr[11] = 0; // Filter
ihdr[12] = 0; // Interlace

// Die Bildzeilen: jede Zeile beginnt mit dem Filter-Byte 0 (None).
const raw = Buffer.alloc(size * (size * 4 + 1));

for (let y = 0; y < size; y++) {
  const rowStart = y * (size * 4 + 1);
  raw[rowStart] = 0;
  scaled.copy(raw, rowStart + 1, y * size * 4, (y + 1) * size * 4);
}

const png = Buffer.concat([
  Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
  chunk('IHDR', ihdr),
  chunk('IDAT', deflateSync(raw, { level: 9 })),
  chunk('IEND', Buffer.alloc(0)),
]);

const here = dirname(fileURLToPath(import.meta.url));
const target = resolve(targetArg ?? `${here}/../src/PACKAGE_ICON.png`);

writeFileSync(target, png);

console.log(`Icon geschrieben: ${target} (${size}×${size}, ${png.length} Bytes)`);
