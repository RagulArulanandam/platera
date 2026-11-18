// Generates original SVG illustrations for seed recipes (no third-party images).
import { mkdirSync, writeFileSync } from 'node:fs';

const out = new URL('../src/Platera.Api/wwwroot/seed/', import.meta.url);
mkdirSync(out, { recursive: true });

const palettes = [
  ['#f6e3c8', '#c4553a'], ['#f3d9b1', '#b8452a'], ['#f7e6c4', '#6b7d3a'], ['#efe0cf', '#a33b2f'],
  ['#eaf0d2', '#6b7d3a'], ['#f8e8d0', '#c98a3d'], ['#e8f0e0', '#4f7a43'], ['#f5e8d8', '#b0703a'],
  ['#f6dfc4', '#d1623a'], ['#ead9cc', '#5a3a2a'], ['#f8e9cf', '#d9a441'], ['#eef0d8', '#7a8f3c'],
];

for (let n = 1; n <= 12; n++) {
  const [bg, accent] = palettes[n - 1];
  const dots = Array.from({ length: 7 }, (_, i) => {
    const x = 120 + ((n * 53 + i * 97) % 560);
    const y = 110 + ((n * 31 + i * 61) % 380);
    const r = 10 + ((n + i * 7) % 22);
    return `<circle cx="${x}" cy="${y}" r="${r}" fill="${accent}" opacity="0.${3 + (i % 4)}"/>`;
  }).join('');
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 600" role="img">
  <rect width="800" height="600" fill="${bg}"/>
  <circle cx="400" cy="310" r="230" fill="#fffaf1"/>
  <circle cx="400" cy="310" r="200" fill="none" stroke="${accent}" stroke-width="6" opacity="0.5"/>
  <circle cx="400" cy="310" r="150" fill="${accent}" opacity="0.88"/>
  ${dots}
  <path d="M400 190 C 450 230, 450 290, 400 340 C 350 290, 350 230, 400 190 Z" fill="#6b7d3a" opacity="0.9"/>
  <path d="M400 200 L400 335" stroke="#fffaf1" stroke-width="4" opacity="0.7"/>
</svg>`;
  writeFileSync(new URL(`recipe-${n}.svg`, out), svg);
}
console.log('seed images generated');
