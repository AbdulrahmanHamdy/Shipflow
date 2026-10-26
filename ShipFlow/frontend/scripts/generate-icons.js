import fs from 'fs';
import path from 'path';
import sharp from 'sharp';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const publicDir = path.resolve(__dirname, '../public');

if (!fs.existsSync(publicDir)) {
  fs.mkdirSync(publicDir, { recursive: true });
}

const masterSvgPath = path.join(publicDir, 'favicon.svg');
const svgBuffer = fs.readFileSync(masterSvgPath);

async function generateIcons() {
  console.log('Generating ShipFlow brand icon and raster assets...');

  // 1. Apple Touch Icon (180x180)
  await sharp(svgBuffer)
    .resize(180, 180)
    .png()
    .toFile(path.join(publicDir, 'apple-touch-icon.png'));
  console.log('✓ Created apple-touch-icon.png (180x180)');

  // 2. PWA icon-192.png
  await sharp(svgBuffer)
    .resize(192, 192)
    .png()
    .toFile(path.join(publicDir, 'icon-192.png'));
  console.log('✓ Created icon-192.png (192x192)');

  // 3. PWA icon-512.png
  await sharp(svgBuffer)
    .resize(512, 512)
    .png()
    .toFile(path.join(publicDir, 'icon-512.png'));
  console.log('✓ Created icon-512.png (512x512)');

  // 4. PWA Maskable Icon 512x512 with background padding
  await sharp({
    create: {
      width: 512,
      height: 512,
      channels: 4,
      background: { r: 11, g: 42, b: 31, alpha: 1 } // Forest Night #0B2A1F
    }
  })
    .composite([
      {
        input: await sharp(svgBuffer).resize(360, 360).png().toBuffer(),
        gravity: 'center'
      }
    ])
    .png()
    .toFile(path.join(publicDir, 'icon-maskable-512.png'));
  console.log('✓ Created icon-maskable-512.png');

  // 5. Favicon.ico (generate as 32x32 PNG renamed or formatted)
  await sharp(svgBuffer)
    .resize(32, 32)
    .png()
    .toFile(path.join(publicDir, 'favicon.ico'));
  console.log('✓ Created favicon.ico (32x32)');

  // 6. Open Graph preview image (1200x630)
  const ogSvg = `
  <svg width="1200" height="630" viewBox="0 0 1200 630" xmlns="http://www.w3.org/2000/svg">
    <rect width="1200" height="630" fill="#0B2A1F"/>
    <defs>
      <linearGradient id="grid-grad" x1="0" y1="0" x2="1" y2="1">
        <stop offset="0%" stop-color="#123B2B" stop-opacity="0.5"/>
        <stop offset="100%" stop-color="#0B2A1F" stop-opacity="1"/>
      </linearGradient>
      <linearGradient id="line-grad" x1="0" y1="0" x2="1" y2="0">
        <stop offset="0%" stop-color="#15803D"/>
        <stop offset="50%" stop-color="#10B981"/>
        <stop offset="100%" stop-color="#DCFCE7"/>
      </linearGradient>
    </defs>
    
    <!-- Subtle Grid Background -->
    <g opacity="0.15" stroke="#10B981" stroke-width="1">
      <line x1="0" y1="105" x2="1200" y2="105"/>
      <line x1="0" y1="210" x2="1200" y2="210"/>
      <line x1="0" y1="315" x2="1200" y2="315"/>
      <line x1="0" y1="420" x2="1200" y2="420"/>
      <line x1="0" y1="525" x2="1200" y2="525"/>
      <line x1="200" y1="0" x2="200" y2="630"/>
      <line x1="400" y1="0" x2="400" y2="630"/>
      <line x1="600" y1="0" x2="600" y2="630"/>
      <line x1="800" y1="0" x2="800" y2="630"/>
      <line x1="1000" y1="0" x2="1000" y2="630"/>
    </g>

    <!-- Visualized Route Line -->
    <path d="M 120 480 Q 380 220 720 340 T 1080 180" fill="none" stroke="url(#line-grad)" stroke-width="6" stroke-linecap="round"/>
    <circle cx="120" cy="480" r="10" fill="#15803D" stroke="#FFFFFF" stroke-width="3"/>
    <circle cx="720" cy="340" r="7" fill="#10B981" stroke="#FFFFFF" stroke-width="2"/>
    <circle cx="1080" cy="180" r="12" fill="#10B981" stroke="#FFFFFF" stroke-width="3"/>

    <!-- Typography -->
    <text x="120" y="160" font-family="Inter, system-ui, sans-serif" font-weight="900" font-size="72" fill="#FFFFFF" letter-spacing="-1">ShipFlow</text>
    <text x="120" y="225" font-family="Inter, system-ui, sans-serif" font-weight="600" font-size="28" fill="#10B981" letter-spacing="0">Smart Logistics &amp; Shipment Management</text>
    <text x="120" y="290" font-family="Inter, system-ui, sans-serif" font-weight="400" font-size="22" fill="#DCE7E0">Every Shipment. One Clear Journey.</text>
    
    <!-- Status Pill -->
    <rect x="120" y="340" width="230" height="42" rx="21" fill="#123B2B" stroke="#10B981" stroke-width="1.5"/>
    <circle cx="144" cy="361" r="5" fill="#10B981"/>
    <text x="160" y="367" font-family="Inter, system-ui, sans-serif" font-weight="600" font-size="16" fill="#DCFCE7">Enterprise Control Tower</text>
  </svg>
  `;

  await sharp(Buffer.from(ogSvg))
    .png()
    .toFile(path.join(publicDir, 'og-image.png'));
  console.log('✓ Created og-image.png (1200x630)');

  // 7. site.webmanifest
  const manifest = {
    name: "ShipFlow Logistics Platform",
    short_name: "ShipFlow",
    description: "Smart Logistics and Shipment Management Platform",
    start_url: "/",
    display: "standalone",
    background_color: "#0B2A1F",
    theme_color: "#15803D",
    icons: [
      {
        src: "/icon-192.png",
        sizes: "192x192",
        type: "image/png"
      },
      {
        src: "/icon-512.png",
        sizes: "512x512",
        type: "image/png"
      },
      {
        src: "/icon-maskable-512.png",
        sizes: "512x512",
        type: "image/png",
        purpose: "maskable"
      }
    ]
  };

  fs.writeFileSync(path.join(publicDir, 'site.webmanifest'), JSON.stringify(manifest, null, 2));
  console.log('✓ Created site.webmanifest');
  console.log('Brand asset generation complete.');
}

generateIcons().catch((err) => {
  console.error('Error generating brand icons:', err);
  process.exit(1);
});
