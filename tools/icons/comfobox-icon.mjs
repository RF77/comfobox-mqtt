// ---------------------------------------------------------------------------
// comfobox-icon.mjs – die **eine** Quelle für das ComfoBox-Markenzeichen.
//
// Warum ein gemeinsames Modul?
//   Dasselbe Zeichen erscheint an drei Stellen: im Browser-Tab (Favicon), im
//   Synology-Paketzentrum (PACKAGE_ICON) und im Kopf der Web-Oberfläche.
//   Vorher zeichnete jedes Werkzeug seine eigene Variante – das lief
//   auseinander. Jetzt liegt die Geometrie hier, und die beiden Erzeuger
//   (`src/ComfoBox.Web/tools/make-icons.mjs` und
//   `deploy/synology-spk/tools/make-icon.mjs`) sind nur noch dünne CLIs.
//
// Design (bewusst schlicht, damit es bei 16 × 16 px lesbar bleibt):
//   abgerundetes Quadrat mit blauem Verlauf, darin ein weißes „C" (Ring mit
//   Öffnung nach rechts) und ein bernsteinfarbener Punkt in der Öffnung.
//
// Das Modul benutzt **nur** Node-Standardmodule (`zlib`) – kein ImageMagick,
// kein Python, keine nativen Abhängigkeiten. Die Rasterung ist reproduzierbar:
// gleiche Parameter ⇒ gleiche Bytes.
// ---------------------------------------------------------------------------

import { deflateSync } from 'node:zlib';

// --- Farben (RGBA) -----------------------------------------------------------
const TOP = [14, 165, 233, 255]; // sky-500
const BOTTOM = [29, 78, 216, 255]; // blue-700
const GLYPH = [255, 255, 255, 255]; // weiß
const ACCENT = [251, 191, 36, 255]; // amber-400 (der Punkt in der Öffnung)

// --- Geometrie (in Design-Einheiten, Bezugsraster 256 × 256) ------------------
export const DESIGN_SIZE = 256;
export const DEFAULT_MARGIN = 10; // Außenrand des Quadrats
export const DEFAULT_CORNER = 54; // Eckenradius

const RING_OUTER = 78; // Außenradius des C
const RING_INNER = 44; // Innenradius des C (Stärke = 34)
const GAP_HALF_ANGLE = 40; // Öffnung des C: ±40° um die positive X-Achse
const DOT_RADIUS = 16;
const RING_CENTER_RADIUS = (RING_OUTER + RING_INNER) / 2;
const DOT_OFFSET_X = RING_CENTER_RADIUS;

const SUPERSAMPLE = 4; // 4 × 4 Abtastpunkte je Pixel (Kantenglättung)

/** Farbverlauf als CSS-Farbe – dieselben Werte wie im SVG. */
export const BRAND_COLORS = {
  top: '#0ea5e9',
  bottom: '#1d4ed8',
  glyph: '#ffffff',
  accent: '#fbbf24',
};

/**
 * Mischt zwei Farben linear.
 *
 * @param {number[]} a Farbe A (RGBA)
 * @param {number[]} b Farbe B (RGBA)
 * @param {number} t Anteil von B (0…1)
 * @returns {number[]} die gemischte Farbe
 */
function mix(a, b, t) {
  const k = Math.min(1, Math.max(0, t));
  return [
    Math.round(a[0] + (b[0] - a[0]) * k),
    Math.round(a[1] + (b[1] - a[1]) * k),
    Math.round(a[2] + (b[2] - a[2]) * k),
    255,
  ];
}

/**
 * Rastert das Markenzeichen.
 *
 * @param {object} [options] Optionen
 * @param {number} [options.size] Kantenlänge in Pixeln (16…2048)
 * @param {number} [options.margin] Außenrand in Design-Einheiten (`0` = randlos)
 * @param {number} [options.corner] Eckenradius in Design-Einheiten (`0` = eckig)
 * @returns {{ size: number, rgba: Buffer }} Raster (RGBA, zeilenweise oben → unten)
 */
export function renderIcon({
  size = DESIGN_SIZE,
  margin = DEFAULT_MARGIN,
  corner = DEFAULT_CORNER,
} = {}) {
  if (!Number.isInteger(size) || size < 16 || size > 2048) {
    throw new Error(`Ungueltige Groesse: ${size} (erlaubt: 16..2048)`);
  }

  const k = size / DESIGN_SIZE; // Design-Einheit → Pixel
  const min = margin * k;
  const max = size - margin * k;
  const radius = corner * k;
  const center = size / 2;

  const ringOuter = RING_OUTER * k;
  const ringInner = RING_INNER * k;
  const dotRadius = DOT_RADIUS * k;
  const dotX = center + DOT_OFFSET_X * k;
  const dotY = center;

  /**
   * Liegt der Punkt im (abgerundeten) Quadrat?
   *
   * @param {number} x X-Koordinate
   * @param {number} y Y-Koordinate
   * @returns {boolean} true, wenn der Punkt zur Fläche gehört
   */
  function insideTile(x, y) {
    if (x < min || x > max || y < min || y > max) {
      return false;
    }

    // Bei `corner = 0` sind dx/dy im Inneren 0 → 0 <= 0, also wahr.
    const dx = Math.max(min + radius - x, 0, x - (max - radius));
    const dy = Math.max(min + radius - y, 0, y - (max - radius));
    return dx * dx + dy * dy <= radius * radius;
  }

  /**
   * Liegt der Punkt auf dem Glyph (Ring oder Punkt)?
   *
   * @param {number} x X-Koordinate
   * @param {number} y Y-Koordinate
   * @returns {number[] | null} Glyph-Farbe oder null
   */
  function glyphAt(x, y) {
    const dotDistance = Math.hypot(x - dotX, y - dotY);
    if (dotDistance <= dotRadius) {
      return ACCENT;
    }

    const dx = x - center;
    const dy = y - center;
    const distance = Math.hypot(dx, dy);

    if (distance < ringInner || distance > ringOuter) {
      return null;
    }

    // Die Öffnung zeigt nach rechts (0°).
    const angle = (Math.atan2(dy, dx) * 180) / Math.PI;
    return Math.abs(angle) <= GAP_HALF_ANGLE ? null : GLYPH;
  }

  /**
   * Farbe eines Abtastpunktes (ohne Kantenglättung).
   *
   * @param {number} x X-Koordinate
   * @param {number} y Y-Koordinate
   * @returns {number[]} RGBA
   */
  function sampleAt(x, y) {
    if (!insideTile(x, y)) {
      return [0, 0, 0, 0];
    }

    const glyph = glyphAt(x, y);
    if (glyph) {
      return glyph;
    }

    const span = size - 2 * margin * k;
    return mix(TOP, BOTTOM, span <= 0 ? 0 : (y - min) / span);
  }

  const rgba = Buffer.alloc(size * size * 4);
  const step = 1 / SUPERSAMPLE;
  const samples = SUPERSAMPLE * SUPERSAMPLE;

  for (let py = 0; py < size; py += 1) {
    for (let px = 0; px < size; px += 1) {
      let sumR = 0;
      let sumG = 0;
      let sumB = 0;
      let sumA = 0;

      for (let sy = 0; sy < SUPERSAMPLE; sy += 1) {
        for (let sx = 0; sx < SUPERSAMPLE; sx += 1) {
          const color = sampleAt(px + (sx + 0.5) * step, py + (sy + 0.5) * step);
          const weight = color[3];
          sumR += color[0] * weight;
          sumG += color[1] * weight;
          sumB += color[2] * weight;
          sumA += weight;
        }
      }

      const offset = (py * size + px) * 4;

      if (sumA === 0) {
        continue; // transparent bleibt transparent (Buffer ist genullt)
      }

      // sumR/sumA ist bereits der mit Alpha gewichtete Mittelwert der Farbe.
      rgba[offset] = Math.round(sumR / sumA);
      rgba[offset + 1] = Math.round(sumG / sumA);
      rgba[offset + 2] = Math.round(sumB / sumA);
      rgba[offset + 3] = Math.round(sumA / samples);
    }
  }

  return { size, rgba };
}

// --- PNG ---------------------------------------------------------------------

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

    for (let bit = 0; bit < 8; bit += 1) {
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
function pngChunk(type, data) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length, 0);

  const typeBuffer = Buffer.from(type, 'ascii');
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([typeBuffer, data])), 0);

  return Buffer.concat([length, typeBuffer, data, crc]);
}

/**
 * Kodiert ein RGBA-Raster als PNG (8 Bit je Kanal, ohne Filter).
 *
 * @param {{ size: number, rgba: Buffer }} image das Raster
 * @returns {Buffer} die PNG-Datei
 */
export function encodePng({ size, rgba }) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8; // Bittiefe
  ihdr[9] = 6; // Farbtyp RGBA
  ihdr[10] = 0; // Kompression (deflate)
  ihdr[11] = 0; // Filterverfahren
  ihdr[12] = 0; // kein Interlace

  // Jede Zeile beginnt mit dem Filter-Byte 0 (None).
  const stride = size * 4 + 1;
  const raw = Buffer.alloc(size * stride);

  for (let y = 0; y < size; y += 1) {
    raw[y * stride] = 0;
    rgba.copy(raw, y * stride + 1, y * size * 4, (y + 1) * size * 4);
  }

  const signature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

  return Buffer.concat([
    signature,
    pngChunk('IHDR', ihdr),
    pngChunk('IDAT', deflateSync(raw, { level: 9 })),
    pngChunk('IEND', Buffer.alloc(0)),
  ]);
}

// --- ICO ---------------------------------------------------------------------

/**
 * Kodiert ein Raster als BMP-Eintrag für eine ICO-Datei.
 *
 * Bewusst **unkomprimiertes BMP** statt PNG: Windows und alle Browser lesen
 * das ohne Sonderfall. Die Zeilen stehen in BMP von unten nach oben.
 *
 * @param {{ size: number, rgba: Buffer }} image das Raster
 * @returns {Buffer} der BMP-Eintrag
 */
function encodeDib({ size, rgba }) {
  const headerSize = 40;
  const xorSize = size * size * 4;
  const maskStride = Math.ceil(size / 32) * 4; // 1 Bit je Pixel, 4-Byte-ausgerichtet
  const andSize = maskStride * size;
  const out = Buffer.alloc(headerSize + xorSize + andSize);

  out.writeUInt32LE(headerSize, 0);
  out.writeInt32LE(size, 4);
  out.writeInt32LE(size * 2, 8); // Höhe zählt XOR- **und** AND-Bitmap
  out.writeUInt16LE(1, 12); // Ebenen
  out.writeUInt16LE(32, 14); // Bit je Pixel
  out.writeUInt32LE(0, 16); // BI_RGB
  out.writeUInt32LE(xorSize, 20);

  for (let y = 0; y < size; y += 1) {
    const source = (size - 1 - y) * size * 4;
    const target = headerSize + y * size * 4;

    for (let x = 0; x < size; x += 1) {
      const s = source + x * 4;
      const t = target + x * 4;
      // BMP speichert BGRA.
      out[t] = rgba[s + 2];
      out[t + 1] = rgba[s + 1];
      out[t + 2] = rgba[s];
      out[t + 3] = rgba[s + 3];
    }
  }

  // Die AND-Maske bleibt 0 („deckend“) – die Transparenz steckt im Alpha-Kanal.
  return out;
}

/**
 * Baut eine ICO-Datei mit mehreren Auflösungen.
 *
 * @param {{ size: number, rgba: Buffer }[]} images die Raster (max. 256 px)
 * @returns {Buffer} die ICO-Datei
 */
export function encodeIco(images) {
  const blobs = images.map((image) => encodeDib(image));

  const header = Buffer.alloc(6);
  header.writeUInt16LE(0, 0); // reserviert
  header.writeUInt16LE(1, 2); // 1 = Icon
  header.writeUInt16LE(images.length, 4);

  const entries = [];
  let offset = header.length + 16 * images.length;

  for (let i = 0; i < images.length; i += 1) {
    const { size } = images[i];
    const entry = Buffer.alloc(16);

    entry[0] = size >= 256 ? 0 : size; // 0 bedeutet 256
    entry[1] = size >= 256 ? 0 : size;
    entry[2] = 0; // keine Palette
    entry[3] = 0;
    entry.writeUInt16LE(1, 4); // Ebenen
    entry.writeUInt16LE(32, 6); // Bit je Pixel
    entry.writeUInt32LE(blobs[i].length, 8);
    entry.writeUInt32LE(offset, 12);

    offset += blobs[i].length;
    entries.push(entry);
  }

  return Buffer.concat([header, ...entries, ...blobs]);
}

// --- SVG ---------------------------------------------------------------------

/**
 * Formatiert eine Zahl für SVG-Attribute (max. zwei Nachkommastellen).
 *
 * @param {number} value der Wert
 * @returns {string} der Text
 */
function num(value) {
  return Number(value.toFixed(2)).toString();
}

/**
 * Erzeugt das Markenzeichen als SVG (dieselbe Geometrie wie das Raster).
 *
 * Das SVG ist die bevorzugte Variante für Favicon und Kopfzeile: es ist bei
 * jeder Auflösung scharf und wiegt nur wenige hundert Byte.
 *
 * @param {object} [options] Optionen
 * @param {number} [options.margin] Außenrand in Design-Einheiten
 * @param {number} [options.corner] Eckenradius in Design-Einheiten
 * @returns {string} das SVG
 */
export function iconSvg({ margin = DEFAULT_MARGIN, corner = DEFAULT_CORNER } = {}) {
  const s = DESIGN_SIZE;
  const c = s / 2;
  const tile = s - 2 * margin;
  const ringRadius = RING_CENTER_RADIUS;
  const strokeWidth = RING_OUTER - RING_INNER;
  const rad = (deg) => (deg * Math.PI) / 180;

  const point = (angle) => [
    c + ringRadius * Math.cos(rad(angle)),
    c + ringRadius * Math.sin(rad(angle)),
  ];

  const [startX, startY] = point(GAP_HALF_ANGLE);
  const [endX, endY] = point(-GAP_HALF_ANGLE);

  // Der Bogen läuft von +40° über 180° nach −40°, also 280° (> 180°).
  const arc = `M ${num(startX)} ${num(startY)} A ${num(ringRadius)} ${num(ringRadius)} 0 1 1 ${num(endX)} ${num(endY)}`;

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${s} ${s}" width="${s}" height="${s}" role="img">
  <title>ComfoBox Gateway</title>
  <defs>
    <linearGradient id="cbk-tile" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="${BRAND_COLORS.top}" />
      <stop offset="1" stop-color="${BRAND_COLORS.bottom}" />
    </linearGradient>
  </defs>
  <rect x="${num(margin)}" y="${num(margin)}" width="${num(tile)}" height="${num(tile)}" rx="${num(corner)}" fill="url(#cbk-tile)" />
  <path d="${arc}" fill="none" stroke="${BRAND_COLORS.glyph}" stroke-width="${num(strokeWidth)}" />
  <circle cx="${num(c + DOT_OFFSET_X)}" cy="${num(c)}" r="${num(DOT_RADIUS)}" fill="${BRAND_COLORS.accent}" />
</svg>
`;
}
