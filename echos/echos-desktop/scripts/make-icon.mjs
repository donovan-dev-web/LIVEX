// Génère build/icon.png (512×512) — icône de repli du shell ECHOS.
//
// Aucune dépendance : encode un PNG RGBA à la main (zlib + CRC32). À relancer
// si la charte LIVEX fournit une icône définitive (déposer alors build/icon.png
// ou build/icon.ico ; electron-builder convertit un PNG ≥ 256 en .ico Windows).
//
// Motif : anneau accent (#38bdf8) autour d'un disque sombre, sur fond nuit.

import { deflateSync } from 'node:zlib'
import { mkdirSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const SIZE = 512
const CENTER = (SIZE - 1) / 2
const OUTER_RADIUS = 150
const INNER_RADIUS = 110

const BG = [0x0b, 0x0f, 0x14]
const ACCENT = [0x38, 0xbd, 0xf8]
const PANEL = [0x12, 0x18, 0x21]

function pixel(x, y) {
  const d = Math.hypot(x - CENTER, y - CENTER)
  if (d >= INNER_RADIUS && d <= OUTER_RADIUS) return ACCENT
  if (d < INNER_RADIUS) return PANEL
  return BG
}

// Trame brute : un octet de filtre (0) puis RGBA par ligne.
const raw = Buffer.alloc(SIZE * (1 + SIZE * 4))
let offset = 0
for (let y = 0; y < SIZE; y += 1) {
  raw[offset] = 0
  offset += 1
  for (let x = 0; x < SIZE; x += 1) {
    const [r, g, b] = pixel(x, y)
    raw[offset] = r
    raw[offset + 1] = g
    raw[offset + 2] = b
    raw[offset + 3] = 0xff
    offset += 4
  }
}

const CRC_TABLE = (() => {
  const table = new Uint32Array(256)
  for (let n = 0; n < 256; n += 1) {
    let c = n
    for (let k = 0; k < 8; k += 1) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1
    table[n] = c >>> 0
  }
  return table
})()

function crc32(buffer) {
  let c = 0xffffffff
  for (let i = 0; i < buffer.length; i += 1) c = CRC_TABLE[(c ^ buffer[i]) & 0xff] ^ (c >>> 8)
  return (c ^ 0xffffffff) >>> 0
}

function chunk(type, data) {
  const length = Buffer.alloc(4)
  length.writeUInt32BE(data.length, 0)
  const body = Buffer.concat([Buffer.from(type, 'ascii'), data])
  const crc = Buffer.alloc(4)
  crc.writeUInt32BE(crc32(body), 0)
  return Buffer.concat([length, body, crc])
}

const ihdr = Buffer.alloc(13)
ihdr.writeUInt32BE(SIZE, 0)
ihdr.writeUInt32BE(SIZE, 4)
ihdr[8] = 8 // profondeur
ihdr[9] = 6 // RGBA
const png = Buffer.concat([
  Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
  chunk('IHDR', ihdr),
  chunk('IDAT', deflateSync(raw, { level: 9 })),
  chunk('IEND', Buffer.alloc(0)),
])

const here = path.dirname(fileURLToPath(import.meta.url))
const buildDir = path.resolve(here, '..', 'build')
mkdirSync(buildDir, { recursive: true })
const target = path.join(buildDir, 'icon.png')
writeFileSync(target, png)
console.log(`[make-icon] ${SIZE}×${SIZE} → ${target}`)
