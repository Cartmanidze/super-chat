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

function iconSvg({ size, padding = 0.18, gradient = "red" }) {
  const inner = Math.round(size * (1 - padding * 2));
  const offset = Math.round((size - inner) / 2);
  const stops =
    gradient === "gold"
      ? `<stop offset="0" stop-color="${ACCENT_GOLD_FROM}"/>
         <stop offset="0.5" stop-color="${ACCENT_GOLD_MID}"/>
         <stop offset="1" stop-color="${ACCENT_GOLD_TO}"/>`
      : `<stop offset="0" stop-color="${ACCENT_RED_FROM}"/>
         <stop offset="1" stop-color="${ACCENT_RED_TO}"/>`;
  const cornerRadius = Math.round(size * 0.22);
  return `
<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
  <defs>
    <linearGradient id="boltGrad" x1="0" y1="0" x2="${size}" y2="${size}" gradientUnits="userSpaceOnUse">${stops}</linearGradient>
    <radialGradient id="bgGlow" cx="${size * 0.2}" cy="${size * 0.15}" r="${size * 0.6}" fx="${size * 0.2}" fy="${size * 0.15}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="rgba(229,56,59,0.55)"/>
      <stop offset="1" stop-color="rgba(229,56,59,0)"/>
    </radialGradient>
  </defs>
  <rect width="${size}" height="${size}" rx="${cornerRadius}" ry="${cornerRadius}" fill="${BG}"/>
  <rect width="${size}" height="${size}" rx="${cornerRadius}" ry="${cornerRadius}" fill="url(#bgGlow)" opacity="0.85"/>
  <g transform="translate(${offset} ${offset}) scale(${inner / 48})">
    <path d="${BOLT_PATH}" fill="url(#boltGrad)" stroke="rgba(255,180,180,0.25)" stroke-width="0.4"/>
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
  const safe = Math.round(size * 0.66);
  const offset = Math.round((size - safe) / 2);
  return `
<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">
  <defs>
    <linearGradient id="boltAdaptive" x1="0" y1="0" x2="${size}" y2="${size}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="${ACCENT_GOLD_FROM}"/>
      <stop offset="0.5" stop-color="${ACCENT_GOLD_MID}"/>
      <stop offset="1" stop-color="${ACCENT_GOLD_TO}"/>
    </linearGradient>
  </defs>
  <g transform="translate(${offset} ${offset}) scale(${safe / 48})">
    <path d="${BOLT_PATH}" fill="url(#boltAdaptive)" stroke="rgba(255,220,140,0.35)" stroke-width="0.4"/>
  </g>
</svg>`;
}

function faviconSvg(size) {
  return iconSvg({ size, padding: 0.12, gradient: "red" });
}

async function svgToPng(svg, size, target) {
  const buffer = Buffer.from(svg);
  await sharp(buffer, { density: 384 }).resize(size, size).png({ quality: 95 }).toFile(target);
}

async function main() {
  await mkdir(out, { recursive: true });

  console.log("• generating icon.png 1024×1024 (red bolt on ink)…");
  await svgToPng(iconSvg({ size: 1024, padding: 0.18, gradient: "red" }), 1024, resolve(out, "icon.png"));

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
