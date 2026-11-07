#!/usr/bin/env node
/**
 * Erzeugt aus docs/benutzer/*.md statische HTML-Seiten für den Hilfe-Bereich
 * der Web-Oberfläche.
 *
 * Aufruf:
 *   node tools/build-docs.mjs [--src docs/benutzer] [--out src/ComfoBox.Web/public/help]
 *
 * Verhalten:
 *   - Jede Seite MUSS ein vollständiges Front-Matter haben (sonst Abbruch).
 *   - Jede Seite MUSS im Index stehen (sonst Abbruch).
 *   - Tote relative Links führen zum Abbruch.
 *   - Erzeugt: <out>/<slug>.html, <out>/index.html, <out>/index.json, <out>/style.css
 *
 * Abhängigkeiten: keine (der Markdown-Parser wird über die Dev-Dependency
 * "marked" des Web-Projekts aufgelöst, siehe resolveParser()).
 */

import { readFile, writeFile, mkdir, rm, readdir } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { dirname, join, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, '..');

// ─────────────────────────────────────────────────────────────────────────────
// Argumente
// ─────────────────────────────────────────────────────────────────────────────
function parseArgs() {
  const args = process.argv.slice(2);
  const opts = {
    src: 'docs/benutzer',
    out: 'src/ComfoBox.Web/public/help',
    version: '1.0.1',
    marked: null,
  };
  for (let i = 0; i < args.length; i += 1) {
    if (args[i] === '--src') opts.src = args[++i];
    else if (args[i] === '--out') opts.out = args[++i];
    else if (args[i] === '--version') opts.version = args[++i];
    else if (args[i] === '--marked') opts.marked = args[++i];
  }
  return opts;
}

const opts = parseArgs();
const srcDir = resolve(repoRoot, opts.src);
const outDir = resolve(repoRoot, opts.out);

// ─────────────────────────────────────────────────────────────────────────────
// Markdown-Parser auflösen
// ─────────────────────────────────────────────────────────────────────────────
async function resolveParser() {
  // marked ist eine Dev-Dependency des Web-Projekts.
  const candidates = [
    opts.marked,
    process.env.MARKED_PATH,
    join(repoRoot, 'src/ComfoBox.Web/node_modules/marked/lib/marked.esm.js'),
    join(repoRoot, 'node_modules/marked/lib/marked.esm.js'),
  ].filter(Boolean);

  for (const candidate of candidates) {
    if (existsSync(candidate)) {
      const mod = await import(`file://${candidate.replace(/\\/g, '/')}`);
      return mod.marked;
    }
  }
  console.error(
    'FEHLER: "marked" nicht gefunden.\n' +
      '  Installieren:  cd src/ComfoBox.Web && npm ci\n' +
      '  Oder:          npm install --save-dev marked\n' +
      '  Oder Pfad:     node tools/build-docs.mjs --marked <pfad/zu/marked.esm.js>',
  );
  process.exit(1);
  return null;
}

// ─────────────────────────────────────────────────────────────────────────────
// Front-Matter
// ─────────────────────────────────────────────────────────────────────────────
const FRONT_MATTER = /^---\r?\n([\s\S]*?)\r?\n---\r?\n?/;

function parseFrontMatter(raw, file) {
  const match = raw.match(FRONT_MATTER);
  if (!match) {
    fail(`${file}: Front-Matter fehlt (muss mit "---\n" beginnen)`);
  }
  const meta = {};
  for (const line of match[1].split(/\r?\n/)) {
    if (!line.trim() || line.trimStart().startsWith('#')) continue;
    const sep = line.indexOf(':');
    if (sep === -1) continue;
    const key = line.slice(0, sep).trim();
    let value = line.slice(sep + 1).trim();
    if (value.startsWith('[') && value.endsWith(']')) {
      value = value
        .slice(1, -1)
        .split(',')
        .map((v) => v.trim().replace(/^["']|["']$/g, ''))
        .filter(Boolean)
        .join(', ');
      meta[key] = value
        .split(', ')
        .map((v) => v.trim())
        .filter(Boolean);
    } else {
      meta[key] = value.replace(/^["']|["']$/g, '');
    }
  }

  for (const required of ['title', 'description', 'order']) {
    if (!meta[required]) {
      fail(`${file}: Front-Matter-Feld "${required}" fehlt`);
    }
  }
  return { meta, body: raw.slice(match[0].length) };
}

// ─────────────────────────────────────────────────────────────────────────────
// Fehler-Sammlung
// ─────────────────────────────────────────────────────────────────────────────
const problems = [];

function fail(message) {
  problems.push(message);
}

// ─────────────────────────────────────────────────────────────────────────────
// Slug
// ─────────────────────────────────────────────────────────────────────────────
function slugify(name) {
  return name
    .toLowerCase()
    .replace(/^\d+-/, '')
    .replace(/\.md$/, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
}

// GitHub-Slug für Überschriften (Anker). "1. Baudrate prüfen" →
// "1-baudrate-prüfen". Umlaute bleiben erhalten (wie auf GitHub).
function slugifyHeading(text) {
  return text
    .toLowerCase()
    .trim()
    .replace(/[^\p{L}\p{N}\s_-]/gu, '')
    .replace(/\s+/g, '-');
}

// Anker aus dem Fließtext holen: "## Adapter erkennen" → "adapter-erkennen"
function extractHeadings(body) {
  const headings = [];
  for (const line of body.split(/\r?\n/)) {
    const match = /^(#{2,4})\s+(.*)$/.exec(line);
    if (!match) continue;
    const text = match[2].replace(/\s*\{#([^}]+)\}\s*$/, '').trim();
    headings.push({ depth: match[1].length, text, slug: slugifyHeading(text) });
  }
  return headings;
}

// ─────────────────────────────────────────────────────────────────────────────
// Link-Prüfung (relative Links innerhalb der Benutzerdoku)
// ─────────────────────────────────────────────────────────────────────────────
function checkLinks(body, file, pages, ownAnchors) {
  const linkRe = /!?\[[^\]]*\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g;
  let match;
  while ((match = linkRe.exec(body)) !== null) {
    const target = match[1];
    if (/^(https?:|mailto:)/.test(target)) continue;

    const [pathPart, rawAnchor] = target.split('#');
    // Anker können URL-kodiert sein ("%C3%BC" für "ü") - für den
    // Vergleich wird der Anker dekodiert.
    const anchor = rawAnchor ? decodeURIComponent(rawAnchor) : undefined;

    // Anker auf dieselbe Seite: muss es dort auch geben.
    if (!pathPart) {
      if (anchor && !ownAnchors.has(anchor)) {
        fail(`${file}: Anker "#${anchor}" existiert auf dieser Seite nicht`);
      }
      continue;
    }

    // Link auf eine andere Seite der Benutzerdoku?
    if (pathPart.endsWith('.md')) {
      const targetSlug = slugify(basename(pathPart));
      const targetPage = pages.get(targetSlug);
      if (!targetPage) {
        fail(`${file}: Link auf unbekannte Seite "${target}"`);
        continue;
      }
      if (anchor && !targetPage.anchors.has(anchor)) {
        fail(`${file}: Anker "#${anchor}" existiert auf "${pathPart}" nicht`);
      }
      continue;
    }

    // Link auf ein Bild im Repo?
    if (/\.(png|jpg|jpeg|gif|svg|webp)$/i.test(pathPart)) {
      const resolved = resolve(dirname(file), pathPart);
      if (!existsSync(resolved)) {
        fail(`${file}: Bild fehlt "${target}"`);
      }
      continue;
    }
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// Interne Links umschreiben: `03-installation.md` → `installation.html`
//
// Wird auf dem **gerenderten** HTML angewendet (marked hat aus
// `[Text](03-installation.md)` bereits `<a href="03-installation.md">`
// gemacht).
// ─────────────────────────────────────────────────────────────────────────────
function rewriteInternalLinks(html) {
  return html.replace(
    /href="([^"#?]+)\.md(#[^"]*)?"/gi,
    (match, path, anchor = '') => {
      // Absolute URLs (https://…) sind externe Links - die bleiben unberührt.
      if (/^[a-z][a-z0-9+.-]*:/i.test(path) || path.startsWith('//')) {
        return match;
      }

      const target = basename(path);
      const slug = target === 'README.md' ? 'index' : slugify(target);
      return `href="${slug}.html${anchor}"`;
    },
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// HTML-Erzeugung
// ─────────────────────────────────────────────────────────────────────────────
function escapeHtml(text) {
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function renderPage({ meta, body, slug, index, version }) {
  const neighbours = index.filter((e) => e.slug !== slug);
  const pos = index.findIndex((e) => e.slug === slug);
  const prev = pos > 0 ? index[pos - 1] : null;
  const next = pos >= 0 && pos < index.length - 1 ? index[pos + 1] : null;

  const nav = neighbours
    .map(
      (e) =>
        `<li><a href="${e.slug}.html">${escapeHtml(e.title)}</a></li>`,
    )
    .join('\n        ');

  const prevLink = prev
    ? `<a class="nav-prev" href="${prev.slug}.html">&larr; ${escapeHtml(prev.title)}</a>`
    : '<span></span>';
  const nextLink = next
    ? `<a class="nav-next" href="${next.slug}.html">${escapeHtml(next.title)} &rarr;</a>`
    : '<span></span>';

  const tagList = (meta.tags ?? [])
    .map((t) => `<span class="tag">${escapeHtml(t)}</span>`)
    .join('');

  return `<!DOCTYPE html>
<html lang="de">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>${escapeHtml(meta.title)} – ComfoBox Gateway</title>
  <meta name="description" content="${escapeHtml(meta.description)}">
  <meta name="generator" content="comfobox-build-docs">
  <link rel="stylesheet" href="style.css">
</head>
<body>
  <div class="layout">
    <nav class="sidebar" aria-label="Hilfe-Navigation">
      <p class="sidebar-title">ComfoBox Hilfe</p>
      <ul>
        ${nav}
      </ul>
    </nav>

    <main class="content">
      <header class="page-header">
        <h1>${escapeHtml(meta.title)}</h1>
        <p class="description">${escapeHtml(meta.description)}</p>
        ${tagList ? `<div class="tags">${tagList}</div>` : ''}
      </header>

      <article class="prose">
${body}
      </article>

      <footer class="page-footer">
        <nav class="pager">
          ${prevLink}
          ${nextLink}
        </nav>
        <p class="version">ComfoBox Gateway ${escapeHtml(version)}</p>
      </footer>
    </main>
  </div>
</body>
</html>
`;
}

/**
 * Das Fragment für die Web-Oberfläche.
 *
 * Die Web-Oberfläche lädt **kein** vollständiges HTML-Dokument, sondern nur
 * den Inhalt der Seite. Ein komplettes Dokument einzufügen würde die
 * Sidebar, den `<head>` und ein zweites `<h1>` in die Oberfläche bringen -
 * der Benutzer sähe die Seite doppelt.
 *
 * Deshalb bekommt jede Seite zusätzlich `<slug>.part.html`:
 *
 * - nur `<h2>`/`<h3>`/`<h4>` mit `id` (Anker für Sprungmarken),
 * - **kein** `<h1>` - die Oberfläche setzt den Titel selbst,
 * - interne Links zeigen auf die SPA-Route `/help/<slug>` statt auf die
 *   statische HTML-Datei,
 * - kein `<html>`, kein `<head>`, keine Sidebar.
 */
function renderFragment({ body }) {
  return `<article class="prose">
${body}
      </article>
`;
}

/**
 * Ersetzt die internen Links (`.md`/`.html`) durch die SPA-Routen
 * (`/help/<slug>`) und hängt die Anker-IDs an die Überschriften.
 */
function prepareFragmentBody(html, headings) {
  let result = html;

  // Anker-IDs an die Überschriften hängen (erste Ebene h2, dann h3, h4 …).
  const byLevel = new Map();
  for (const heading of headings) {
    if (!byLevel.has(heading.depth)) byLevel.set(heading.depth, []);
    byLevel.get(heading.depth).push(heading);
  }

  // Der Renderer von marked erzeugt die Überschriften in Dokumentreihenfolge.
  // Wir zählen sie deshalb pro Ebene durch.
  const counters = new Map();
  const usedIds = new Set();
  result = result.replace(
    /<h([2-4])>([\s\S]*?)<\/h\1>/g,
    (match, level, inner) => {
      const depth = Number(level);
      const index = counters.get(depth) ?? 0;
      counters.set(depth, index + 1);
      const heading = byLevel.get(depth)?.[index];

      if (!heading) {
        return match;
      }

      // Der Text der Überschrift steht im gerenderten HTML – der Slug wird
      // daraus berechnet, damit er zum Link-Anker passt.
      const plain = inner
        .replace(/<[^>]+>/g, '')
        .replace(/\s*\{#([^}]+)\}\s*$/, '')
        .trim();
      const explicit = /\{#([^}]+)\}/.exec(inner);
      let id = explicit ? explicit[1] : slugifyHeading(plain);

      // Doppelte Anker (zwei gleiche Überschriften) müssen eindeutig sein.
      if (usedIds.has(id)) {
        let suffix = 2;
        while (usedIds.has(`${id}-${suffix}`)) suffix += 1;
        id = `${id}-${suffix}`;
      }
      usedIds.add(id);

      return `<h${level} id="${id}">${inner}</h${level}>`;
    },
  );

  // Interne Links auf die SPA-Route umschreiben.
  //
  // Die Quell-Links zeigen auf `.md` (aus der Benutzerdoku) bzw. auf die
  // statische HTML-Datei. In der Oberfläche müssen sie auf die Route
  // `/help/<slug>` zeigen, sonst verlassen sie die Anwendung.
  result = result.replace(
    /href="([^"#?]+)\.(?:md|html)(#[^"]*)?"/gi,
    (_match, slug, anchor = '') => {
      // Anker dekodieren ("%C3%BC" → "ü"), damit er zur ID passt.
      let clean = anchor;
      try {
        clean = anchor ? `#${decodeURIComponent(anchor.slice(1))}` : '';
      } catch {
        clean = anchor;
      }
      return `href="/help/${slug}${clean}"`;
    },
  );

  // Die `<h1>` der Seite entfernen - die Oberfläche setzt den Titel selbst
  // (sonst stünde er zweimal da).
  result = result.replace(/^\s*<h1>[\s\S]*?<\/h1>\s*/i, '');

  return result;
}

const STYLE = `/* Erzeugt von tools/build-docs.mjs – nicht von Hand bearbeiten. */
:root {
  --cb-fg: #1f2933;
  --cb-muted: #52606d;
  --cb-bg: #ffffff;
  --cb-accent: #0f766e;
  --cb-border: #e4e7eb;
  --cb-code-bg: #f5f7fa;
}

@media (prefers-color-scheme: dark) {
  :root {
    --cb-fg: #e4e7eb;
    --cb-muted: #9aa5b1;
    --cb-bg: #111827;
    --cb-accent: #5eead4;
    --cb-border: #374151;
    --cb-code-bg: #1f2937;
  }
}

* { box-sizing: border-box; }

body {
  margin: 0;
  font-family: system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
  line-height: 1.65;
  color: var(--cb-fg);
  background: var(--cb-bg);
}

.layout { display: flex; min-height: 100vh; }

.sidebar {
  flex: 0 0 260px;
  border-right: 1px solid var(--cb-border);
  padding: 1.5rem 1rem;
  overflow-y: auto;
}

.sidebar-title { font-weight: 600; margin: 0 0 1rem; }

.sidebar ul { list-style: none; margin: 0; padding: 0; }
.sidebar li { margin: 0.15rem 0; }
.sidebar a {
  display: block;
  padding: 0.35rem 0.6rem;
  border-radius: 0.375rem;
  color: var(--cb-muted);
  text-decoration: none;
  font-size: 0.925rem;
}
.sidebar a:hover { background: var(--cb-code-bg); color: var(--cb-fg); }

.content { flex: 1; padding: 2rem 2.5rem 4rem; max-width: 60rem; }

.page-header { border-bottom: 1px solid var(--cb-border); padding-bottom: 1rem; margin-bottom: 1.5rem; }
.page-header h1 { margin: 0 0 0.35rem; font-size: 1.9rem; }
.description { margin: 0; color: var(--cb-muted); }

.tags { margin-top: 0.75rem; display: flex; gap: 0.4rem; flex-wrap: wrap; }
.tag {
  font-size: 0.75rem; padding: 0.15rem 0.5rem; border-radius: 999px;
  background: var(--cb-code-bg); color: var(--cb-muted); border: 1px solid var(--cb-border);
}

.prose h2 { margin-top: 2.25rem; font-size: 1.35rem; }
.prose h3 { margin-top: 1.5rem; font-size: 1.1rem; }
.prose table { border-collapse: collapse; width: 100%; margin: 1rem 0; font-size: 0.925rem; }
.prose th, .prose td { border: 1px solid var(--cb-border); padding: 0.45rem 0.6rem; text-align: left; vertical-align: top; }
.prose th { background: var(--cb-code-bg); font-weight: 600; }
.prose code { background: var(--cb-code-bg); padding: 0.1rem 0.35rem; border-radius: 0.25rem; font-size: 0.9em; }
.prose pre { background: var(--cb-code-bg); padding: 0.9rem 1rem; border-radius: 0.5rem; overflow-x: auto; }
.prose pre code { background: none; padding: 0; }
.prose blockquote {
  margin: 1rem 0; padding: 0.6rem 1rem; border-left: 3px solid var(--cb-accent);
  background: var(--cb-code-bg); border-radius: 0 0.375rem 0.375rem 0;
}
.prose img { max-width: 100%; height: auto; border-radius: 0.375rem; }

.page-footer { margin-top: 3rem; border-top: 1px solid var(--cb-border); padding-top: 1rem; }
.pager { display: flex; justify-content: space-between; font-size: 0.95rem; }
.pager a { color: var(--cb-accent); text-decoration: none; }
.version { color: var(--cb-muted); font-size: 0.8rem; margin-top: 1rem; }

@media (max-width: 780px) {
  .layout { flex-direction: column; }
  .sidebar { flex: none; border-right: none; border-bottom: 1px solid var(--cb-border); }
  .content { padding: 1.25rem; }
}

@media print {
  .sidebar, .pager { display: none; }
  .content { max-width: none; padding: 0; }
  body { background: #fff; color: #000; }
}
`;

// ─────────────────────────────────────────────────────────────────────────────
// Hauptlauf
// ─────────────────────────────────────────────────────────────────────────────
async function main() {
  if (!existsSync(srcDir)) {
    console.error(`FEHLER: Quellverzeichnis nicht gefunden: ${srcDir}`);
    process.exit(1);
  }

  const marked = await resolveParser();

  const files = (await readdir(srcDir))
    .filter((f) => f.endsWith('.md'))
    .sort();

  if (files.length === 0) {
    console.error(`FEHLER: keine .md-Dateien in ${srcDir}`);
    process.exit(1);
  }

  const pages = [];
  const pagesBySlug = new Map();

  for (const file of files) {
    const fullPath = join(srcDir, file);
    const raw = await readFile(fullPath, 'utf8');
    const { meta, body } = parseFrontMatter(raw, file);
    const slug = file === 'README.md' ? 'index' : slugify(file);
    const headings = extractHeadings(body);
    const anchors = new Set(headings.map((h) => h.slug));
    pages.push({ file, fullPath, meta, body, slug, headings, anchors });
    pagesBySlug.set(slug, pages[pages.length - 1]);
  }

  // Links prüfen (alle Slugs sind jetzt bekannt)
  for (const page of pages) {
    checkLinks(page.body, page.file, pagesBySlug, page.anchors);
  }

  // Sortieren
  pages.sort((a, b) => Number(a.meta.order) - Number(b.meta.order));

  // Jede Seite muss im README-Index stehen
  const readme = pages.find((p) => p.slug === 'index');
  if (!readme) {
    fail('README.md fehlt in docs/benutzer/ – die Startseite ist Pflicht');
  } else {
    for (const page of pages) {
      if (page.slug === 'index') continue;
      if (!readme.body.includes(`${page.slug}.html`) && !readme.body.includes(page.file)) {
        fail(`README.md: Seite "${page.file}" ist nicht im Inhaltsverzeichnis verlinkt`);
      }
    }
  }

  if (problems.length > 0) {
    console.error(`\nFEHLER – ${problems.length} Problem(e) in der Benutzerdoku:\n`);
    for (const p of problems) console.error(`  - ${p}`);
    console.error('');
    process.exit(1);
  }

  // Ausgabe vorbereiten
  await rm(outDir, { recursive: true, force: true });
  await mkdir(outDir, { recursive: true });

  const index = pages.map((p) => ({
    slug: p.slug,
    title: p.meta.title,
    description: p.meta.description,
    order: Number(p.meta.order),
    icon: p.meta.icon ?? null,
    tags: p.meta.tags ?? [],
  }));

  const indexJson = [];
  for (const page of pages) {
    const rendered = marked.parse(page.body, { gfm: true, breaks: false });
    const html = rewriteInternalLinks(rendered);
    const plain = page.body
      .replace(/```[\s\S]*?```/g, ' ')
      .replace(/`[^`]*`/g, ' ')
      .replace(/!?\[([^\]]*)\]\([^)]*\)/g, '$1')
      .replace(/[#*_>|]/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();

    const fullHtml = renderPage({
      meta: page.meta,
      body: html,
      slug: page.slug,
      index,
      version: opts.version,
    });
    await writeFile(join(outDir, `${page.slug}.html`), fullHtml, 'utf8');

    // Das Fragment für die Web-Oberfläche: nur der Inhalt, mit Anker-IDs
    // und mit Links auf die SPA-Routen.
    const fragment = renderFragment({
      body: prepareFragmentBody(html, page.headings),
    });
    await writeFile(join(outDir, `${page.slug}.part.html`), fragment, 'utf8');

    indexJson.push({
      ...index.find((e) => e.slug === page.slug),
      content: plain,
    });
  }

  await writeFile(join(outDir, 'style.css'), STYLE, 'utf8');
  await writeFile(
    join(outDir, 'index.json'),
    `${JSON.stringify(indexJson, null, 2)}\n`,
    'utf8',
  );

  console.log(`Benutzerdoku: ${pages.length} Seiten → ${outDir}`);
  for (const page of pages) {
    console.log(`  ${page.slug}.html  (${page.meta.title})`);
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
