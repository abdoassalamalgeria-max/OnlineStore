// Generates the home slider illustrations (original vector art, no text) into public/slides/.
// Run: node scripts/generate-slides.mjs  — deterministic, so the output only changes with this file.
import { mkdirSync, writeFileSync } from "node:fs";

const W = 1920;
const H = 640;
const out = new URL("../public/slides/", import.meta.url);
mkdirSync(out, { recursive: true });

// Small deterministic PRNG so every run draws the same shelves.
function rng(seed) {
  let s = seed >>> 0;
  return () => (s = (s * 1664525 + 1013904223) >>> 0) / 2 ** 32;
}
const pick = (r, arr) => arr[Math.floor(r() * arr.length)];

const spineColors = [
  "#7a2e2e",
  "#8c3b2f",
  "#2f4a6b",
  "#1f3a4f",
  "#2a5934",
  "#3d6b45",
  "#c2a02e",
  "#a8862a",
  "#e9e1cf",
  "#d8cdb4",
  "#5a3e66",
  "#2e6b6b",
  "#b5653a",
  "#4a4a4a",
  "#9c2f45",
  "#6b8f71",
];

function bookRow(r, x0, x1, baseY, maxH) {
  let x = x0;
  let svg = "";
  while (x < x1 - 30) {
    const w = 18 + Math.floor(r() * 26);
    const h = maxH * (0.72 + r() * 0.28);
    const color = pick(r, spineColors);
    const lean = r() < 0.06 && x + w + 40 < x1;
    const y = baseY - h;
    const g = lean ? `transform="rotate(-9 ${x + w} ${baseY})"` : "";
    svg += `<g ${g}>`;
    svg += `<rect x="${x}" y="${y.toFixed(1)}" width="${w}" height="${h.toFixed(1)}" rx="2" fill="${color}"/>`;
    // Spine details: gold bands and a darker edge.
    const band = r() < 0.7;
    if (band) {
      svg += `<rect x="${x + 2}" y="${(y + h * 0.12).toFixed(1)}" width="${w - 4}" height="3" fill="#e4c766" opacity=".75"/>`;
      svg += `<rect x="${x + 2}" y="${(y + h * 0.82).toFixed(1)}" width="${w - 4}" height="3" fill="#e4c766" opacity=".6"/>`;
    }
    if (r() < 0.35) {
      svg += `<rect x="${x + w * 0.25}" y="${(y + h * 0.35).toFixed(1)}" width="${w * 0.5}" height="${(h * 0.18).toFixed(1)}" rx="1" fill="#000" opacity=".18"/>`;
    }
    svg += `<rect x="${x + w - 3}" y="${y.toFixed(1)}" width="3" height="${h.toFixed(1)}" fill="#000" opacity=".18"/>`;
    svg += `</g>`;
    x += w + (lean ? 26 : 1 + Math.floor(r() * 2));
    // Occasional gap with a small decorative object.
    if (r() < 0.04) {
      svg += `<circle cx="${x + 22}" cy="${baseY - 20}" r="16" fill="#c2a02e" opacity=".85"/>`;
      x += 48;
    }
  }
  return svg;
}

function shelves() {
  const r = rng(7);
  let svg = `<defs>
    <linearGradient id="wall" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#1e4226"/><stop offset="1" stop-color="#132c19"/></linearGradient>
    <radialGradient id="glow" cx=".7" cy=".25" r=".7"><stop offset="0" stop-color="#f3d98a" stop-opacity=".35"/><stop offset="1" stop-color="#f3d98a" stop-opacity="0"/></radialGradient>
    <linearGradient id="wood" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#8a5a32"/><stop offset="1" stop-color="#5e3b1f"/></linearGradient>
  </defs>
  <rect width="${W}" height="${H}" fill="url(#wall)"/>`;
  const rows = [190, 400, 610];
  for (const baseY of rows) {
    svg += bookRow(r, 40, W - 40, baseY, 170);
    svg += `<rect x="0" y="${baseY}" width="${W}" height="22" fill="url(#wood)"/>`;
    svg += `<rect x="0" y="${baseY + 22}" width="${W}" height="8" fill="#000" opacity=".25"/>`;
  }
  // Vertical uprights.
  for (const x of [0, 640, 1280, W - 26]) {
    svg += `<rect x="${x}" y="0" width="26" height="${H}" fill="url(#wood)"/><rect x="${x + 22}" y="0" width="4" height="${H}" fill="#000" opacity=".2"/>`;
  }
  svg += `<rect width="${W}" height="${H}" fill="url(#glow)"/>`;
  return svg;
}

// Eight-point star (khatam) tile, the classic Maghrebi geometric motif.
function starPattern(id, color, opacity) {
  const s = 80;
  const c = s / 2;
  const r1 = 26;
  const r2 = 12;
  let pts = "";
  for (let i = 0; i < 16; i++) {
    const a = (Math.PI / 8) * i - Math.PI / 2;
    const rad = i % 2 === 0 ? r1 : r2;
    pts += `${(c + rad * Math.cos(a)).toFixed(1)},${(c + rad * Math.sin(a)).toFixed(1)} `;
  }
  return `<pattern id="${id}" width="${s}" height="${s}" patternUnits="userSpaceOnUse">
    <polygon points="${pts.trim()}" fill="none" stroke="${color}" stroke-width="1.6" opacity="${opacity}"/>
    <circle cx="${c}" cy="${c}" r="5" fill="none" stroke="${color}" stroke-width="1.2" opacity="${opacity}"/>
  </pattern>`;
}

function stackedBook(x, y, w, h, color) {
  return `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="4" fill="${color}"/>
    <rect x="${x + 8}" y="${y + 4}" width="${w - 16}" height="${h - 8}" rx="2" fill="#f4eedf"/>
    <rect x="${x + 8}" y="${y + h / 2 - 1}" width="${w - 16}" height="2" fill="#d9cfb6"/>
    <rect x="${x}" y="${y}" width="${w * 0.06}" height="${h}" rx="3" fill="#000" opacity=".2"/>`;
}

function openBook(cx, cy, scale) {
  const w = 300 * scale;
  const h = 170 * scale;
  let lines = "";
  for (let i = 0; i < 7; i++) {
    const y = cy - h * 0.62 + i * h * 0.12;
    lines += `<path d="M ${cx - w * 0.82} ${y + 6} Q ${cx - w * 0.45} ${y - 4} ${cx - 14} ${y + 4}" stroke="#cbbf9f" stroke-width="3" fill="none"/>`;
    lines += `<path d="M ${cx + 14} ${y + 4} Q ${cx + w * 0.45} ${y - 4} ${cx + w * 0.82} ${y + 6}" stroke="#cbbf9f" stroke-width="3" fill="none"/>`;
  }
  return `<path d="M ${cx} ${cy} Q ${cx - w * 0.5} ${cy - h * 0.25} ${cx - w} ${cy - h * 0.05} L ${cx - w} ${cy - h} Q ${cx - w * 0.5} ${cy - h * 1.2} ${cx} ${cy - h * 0.95} Z" fill="#fbf7ec"/>
    <path d="M ${cx} ${cy} Q ${cx + w * 0.5} ${cy - h * 0.25} ${cx + w} ${cy - h * 0.05} L ${cx + w} ${cy - h} Q ${cx + w * 0.5} ${cy - h * 1.2} ${cx} ${cy - h * 0.95} Z" fill="#f6f0e0"/>
    ${lines}
    <path d="M ${cx} ${cy} L ${cx} ${cy - h * 0.95}" stroke="#bfae84" stroke-width="3"/>
    <path d="M ${cx - w - 10} ${cy + 4} Q ${cx - w * 0.5} ${cy - h * 0.18} ${cx} ${cy + 10} Q ${cx + w * 0.5} ${cy - h * 0.18} ${cx + w + 10} ${cy + 4}" stroke="#7a2e2e" stroke-width="12" fill="none" stroke-linecap="round"/>`;
}

function literature() {
  let svg = `<defs>
    <linearGradient id="bg2" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#7a2e2e"/><stop offset="1" stop-color="#3e1517"/></linearGradient>
    ${starPattern("stars", "#e4c766", 0.22)}
    <radialGradient id="light2" cx=".72" cy=".45" r=".45"><stop offset="0" stop-color="#ffe9a8" stop-opacity=".45"/><stop offset="1" stop-color="#ffe9a8" stop-opacity="0"/></radialGradient>
  </defs>
  <rect width="${W}" height="${H}" fill="url(#bg2)"/>
  <rect width="${W}" height="${H}" fill="url(#stars)"/>
  <rect width="${W}" height="${H}" fill="url(#light2)"/>`;
  // Book stack on the visual right third (text sits on the opposite side in either direction via overlay).
  const cx = 1380;
  const base = 600;
  const stack = [
    [-210, 70, "#2a5934"],
    [-190, 62, "#c2a02e"],
    [-225, 74, "#1f3a4f"],
    [-200, 60, "#e9e1cf"],
    [-215, 66, "#2e6b6b"],
  ];
  let y = base;
  stack.forEach(([dx, h, color], i) => {
    y -= h;
    const w = Math.abs(dx) * 2;
    svg += stackedBook(cx - w / 2 + (i % 2 ? 14 : -10), y, w, h, color);
  });
  svg += openBook(cx, y - 6, 0.95);
  // Floating stars of light.
  const r = rng(11);
  for (let i = 0; i < 26; i++) {
    svg += `<circle cx="${(1000 + r() * 820).toFixed(0)}" cy="${(40 + r() * 300).toFixed(0)}" r="${(1.5 + r() * 3).toFixed(1)}" fill="#ffe9a8" opacity="${(0.4 + r() * 0.5).toFixed(2)}"/>`;
  }
  return svg;
}

function delivery() {
  let svg = `<defs>
    <linearGradient id="bg3" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#1f3a4f"/><stop offset="1" stop-color="#2a5934"/></linearGradient>
    ${starPattern("stars3", "#ffffff", 0.07)}
  </defs>
  <rect width="${W}" height="${H}" fill="url(#bg3)"/>
  <rect width="${W}" height="${H}" fill="url(#stars3)"/>`;
  // Dotted route with location pins across the slide.
  svg += `<path d="M 220 520 C 520 360, 760 600, 1020 430 S 1500 260, 1760 360" fill="none" stroke="#e4c766" stroke-width="5" stroke-dasharray="2 18" stroke-linecap="round" opacity=".9"/>`;
  for (const [x, y] of [
    [220, 520],
    [1020, 430],
    [1760, 360],
  ]) {
    svg += `<g transform="translate(${x} ${y})"><path d="M0 0 C -22 -30, -22 -58, 0 -64 C 22 -58, 22 -30, 0 0 Z" fill="#c2a02e"/><circle cx="0" cy="-42" r="9" fill="#1e4226"/></g>`;
  }
  // Open parcel with books.
  const bx = 1240;
  const by = 330;
  svg += `<rect x="${bx}" y="${by + 120}" width="360" height="170" rx="8" fill="#c89a5b"/>
    <rect x="${bx}" y="${by + 120}" width="360" height="26" fill="#000" opacity=".12"/>
    <rect x="${bx + 160}" y="${by + 120}" width="40" height="170" fill="#e8d3a4" opacity=".8"/>
    <polygon points="${bx},${by + 120} ${bx - 60},${by + 70} ${bx + 120},${by + 70} ${bx + 170},${by + 120}" fill="#d9ab6a"/>
    <polygon points="${bx + 360},${by + 120} ${bx + 420},${by + 70} ${bx + 240},${by + 70} ${bx + 190},${by + 120}" fill="#b98a4c"/>`;
  const books = [
    ["#7a2e2e", 40, 150],
    ["#e9e1cf", 32, 130],
    ["#2a5934", 44, 165],
    ["#c2a02e", 30, 120],
    ["#2f4a6b", 38, 145],
  ];
  let x = bx + 60;
  for (const [color, w, h] of books) {
    svg += `<rect x="${x}" y="${by + 128 - h}" width="${w}" height="${h}" rx="3" fill="${color}"/><rect x="${x + 3}" y="${by + 140 - h}" width="${w - 6}" height="3" fill="#e4c766" opacity=".7"/>`;
    x += w + 6;
  }
  return svg;
}

const wrap = (body) =>
  `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}" preserveAspectRatio="xMidYMid slice">${body}</svg>\n`;

writeFileSync(new URL("shelves.svg", out), wrap(shelves()));
writeFileSync(new URL("literature.svg", out), wrap(literature()));
writeFileSync(new URL("delivery.svg", out), wrap(delivery()));
console.log("slides written to public/slides/");
