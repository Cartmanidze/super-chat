// Generates brand PNG assets from inline SVG, using sharp.
// Run: node tools/generate-assets.mjs

import { mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import sharp from "sharp";

const here = dirname(fileURLToPath(import.meta.url));
const out = resolve(here, "..", "assets");

const BG = "#060606";
const ACCENT_RED_FROM = "#e5383b";
const ACCENT_RED_TO = "#c1121f";
const ACCENT_GOLD_FROM = "#ffe49a";
const ACCENT_GOLD_MID = "#f3c96b";
const ACCENT_GOLD_TO = "#a67c1e";

const BOLT_PATH = "M28 4 L14 26 L21 26 L19 44 L34 20 L27 20 L28 4 Z";

function iconSvg({ size, padding = 0.18 }) {
  const inner = Math.round(size * (1 - padding * 2));
  const offset = Math.round((size - inner) / 2);
  const cornerRadius = Math.round(size * 0.22);
  return `
<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
  <defs>
    <!-- Base diagonal gradient: bolt red → deep red-black, как у BoltChip в приложении -->
    <linearGradient id="brandBase" x1="0" y1="0" x2="${size}" y2="${size}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="${ACCENT_RED_FROM}"/>
      <stop offset="0.55" stop-color="#8e0e1a"/>
      <stop offset="1" stop-color="#1a0307"/>
    </linearGradient>
    <!-- Soft red glow in the upper-left, как radial-gradient(120% 100% at 20% 10%, #ff6b6d) -->
    <radialGradient id="topGlow" cx="${size * 0.2}" cy="${size * 0.1}" r="${size * 0.85}" fx="${size * 0.2}" fy="${size * 0.1}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="rgba(255,107,109,0.75)"/>
      <stop offset="0.55" stop-color="rgba(255,107,109,0)"/>
    </radialGradient>
    <!-- Subtle black corner shading -->
    <radialGradient id="darkCorner" cx="${size * 0.88}" cy="${size * 0.92}" r="${size * 0.7}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="rgba(0,0,0,0)"/>
      <stop offset="1" stop-color="rgba(0,0,0,0.55)"/>
    </radialGradient>
    <linearGradient id="boltGold" x1="0" y1="0" x2="${size}" y2="${size}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="${ACCENT_GOLD_FROM}"/>
      <stop offset="0.5" stop-color="${ACCENT_GOLD_MID}"/>
      <stop offset="1" stop-color="${ACCENT_GOLD_TO}"/>
    </linearGradient>
    <filter id="boltGlow" x="-30%" y="-30%" width="160%" height="160%">
      <feGaussianBlur in="SourceGraphic" stdDeviation="${size * 0.012}" result="blur"/>
      <feMerge>
        <feMergeNode in="blur"/>
        <feMergeNode in="SourceGraphic"/>
      </feMerge>
    </filter>
  </defs>
  <rect width="${size}" height="${size}" rx="${cornerRadius}" ry="${cornerRadius}" fill="url(#brandBase)"/>
  <rect width="${size}" height="${size}" rx="${cornerRadius}" ry="${cornerRadius}" fill="url(#topGlow)"/>
  <rect width="${size}" height="${size}" rx="${cornerRadius}" ry="${cornerRadius}" fill="url(#darkCorner)"/>
  <rect width="${size}" height="${size}" rx="${cornerRadius}" ry="${cornerRadius}" fill="none" stroke="rgba(255,255,255,0.08)" stroke-width="${Math.max(1, size * 0.003)}"/>
  <g transform="translate(${offset} ${offset}) scale(${inner / 48})" filter="url(#boltGlow)">
    <path d="${BOLT_PATH}" fill="url(#boltGold)" stroke="rgba(255,220,140,0.5)" stroke-width="0.5"/>
  </g>
</svg>`;
}

function splashSvg(size) {
  const boltSize = Math.round(size * 0.32);
  const boltX = Math.round((size - boltSize) / 2);
  const boltY = Math.round((size - boltSize) / 2);
  return `
<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
  <defs>
    <linearGradient id="boltSplash" x1="0" y1="0" x2="${size}" y2="${size}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="${ACCENT_GOLD_FROM}"/>
      <stop offset="0.5" stop-color="${ACCENT_GOLD_MID}"/>
      <stop offset="1" stop-color="${ACCENT_GOLD_TO}"/>
    </linearGradient>
    <radialGradient id="splashGlow" cx="${size * 0.5}" cy="${size * 0.5}" r="${size * 0.45}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="rgba(229,56,59,0.3)"/>
      <stop offset="1" stop-color="rgba(229,56,59,0)"/>
    </radialGradient>
  </defs>
  <rect width="${size}" height="${size}" fill="${BG}"/>
  <rect width="${size}" height="${size}" fill="url(#splashGlow)"/>
  <g transform="translate(${boltX} ${boltY}) scale(${boltSize / 48})">
    <path d="${BOLT_PATH}" fill="url(#boltSplash)" stroke="rgba(255,220,140,0.35)" stroke-width="0.4"/>
  </g>
</svg>`;
}

function adaptiveForegroundSvg(size) {
  // Android adaptive icon: foreground occupies 66% (safe zone), system mask
  // crops the corners. We draw a red→dark-red round plate + gold bolt.
  const safe = Math.round(size * 0.66);
  const offset = Math.round((size - safe) / 2);
  const radius = Math.round(safe * 0.5);
  const boltSize = Math.round(safe * 0.7);
  const boltOffset = Math.round((safe - boltSize) / 2);
  return `
<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
  <defs>
    <linearGradient id="adBase" x1="0" y1="0" x2="${safe}" y2="${safe}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="${ACCENT_RED_FROM}"/>
      <stop offset="0.6" stop-color="#8e0e1a"/>
      <stop offset="1" stop-color="#1a0307"/>
    </linearGradient>
    <radialGradient id="adGlow" cx="${safe * 0.25}" cy="${safe * 0.18}" r="${safe * 0.75}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="rgba(255,107,109,0.7)"/>
      <stop offset="1" stop-color="rgba(255,107,109,0)"/>
    </radialGradient>
    <linearGradient id="adGold" x1="0" y1="0" x2="${boltSize}" y2="${boltSize}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="${ACCENT_GOLD_FROM}"/>
      <stop offset="0.5" stop-color="${ACCENT_GOLD_MID}"/>
      <stop offset="1" stop-color="${ACCENT_GOLD_TO}"/>
    </linearGradient>
  </defs>
  <g transform="translate(${offset} ${offset})">
    <circle cx="${safe / 2}" cy="${safe / 2}" r="${radius}" fill="url(#adBase)"/>
    <circle cx="${safe / 2}" cy="${safe / 2}" r="${radius}" fill="url(#adGlow)"/>
    <g transform="translate(${boltOffset} ${boltOffset}) scale(${boltSize / 48})">
      <path d="${BOLT_PATH}" fill="url(#adGold)" stroke="rgba(255,220,140,0.45)" stroke-width="0.5"/>
    </g>
  </g>
</svg>`;
}

function faviconSvg(size) {
  return iconSvg({ size, padding: 0.12 });
}

async function svgToPng(svg, size, target) {
  const buffer = Buffer.from(svg);
  await sharp(buffer, { density: 384 }).resize(size, size).png({ quality: 95 }).toFile(target);
}

async function main() {
  await mkdir(out, { recursive: true });

  console.log("• generating icon.png 1024×1024 (gold bolt on red)…");
  await svgToPng(iconSvg({ size: 1024, padding: 0.18 }), 1024, resolve(out, "icon.png"));

  console.log("• generating splash-icon.png 1242×1242 (gold bolt centered)…");
  await svgToPng(splashSvg(1242), 1242, resolve(out, "splash-icon.png"));

  console.log("• generating adaptive-icon.png 1024×1024 (gold bolt foreground)…");
  await svgToPng(adaptiveForegroundSvg(1024), 1024, resolve(out, "adaptive-icon.png"));

  console.log("• generating favicon.png 48×48 (red bolt small)…");
  await svgToPng(faviconSvg(48), 48, resolve(out, "favicon.png"));

  console.log("✓ done");
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
