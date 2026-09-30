// Build du backend ECHOS (PyInstaller, onedir) pour le shell Electron.
//
// Produit echos-desktop/resources/backend/echos-server/ à partir du spec
// echos-desktop/backend/echos-server.spec. À lancer depuis echos-desktop :
//   npm run build:backend
//
// Le Python utilisé est celui du venv ECHOS (.venv), ou la variable
// d'environnement ECHOS_PYTHON. PyInstaller ne cross-compile pas : le
// binaire produit est celui de la plateforme courante.

import { execFileSync } from 'node:child_process'
import { existsSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const here = path.dirname(fileURLToPath(import.meta.url))
const desktopDir = path.resolve(here, '..')
const echosDir = path.resolve(desktopDir, '..')
const spec = path.join(desktopDir, 'backend', 'echos-server.spec')
const output = path.join(desktopDir, 'resources', 'backend', 'echos-server')

const venvPython = process.platform === 'win32'
  ? path.join(echosDir, '.venv', 'Scripts', 'python.exe')
  : path.join(echosDir, '.venv', 'bin', 'python')
const fromEnv = process.env.ECHOS_PYTHON
const python = fromEnv || venvPython

if (!fromEnv && !existsSync(python)) {
  console.error(`[build-backend] interpréteur introuvable : ${python}`)
  console.error('[build-backend] créez echos/.venv ou définissez ECHOS_PYTHON')
  process.exit(1)
}

const args = [
  '-m', 'PyInstaller',
  '--noconfirm',
  '--clean',
  '--distpath', path.join(desktopDir, 'resources', 'backend'),
  '--workpath', path.join(desktopDir, 'build', 'pyinstaller'),
  spec,
]

console.log(`[build-backend] PyInstaller via ${python}`)
execFileSync(python, args, { stdio: 'inherit', cwd: echosDir })

if (!existsSync(output)) {
  console.error(`[build-backend] sortie absente : ${output}`)
  process.exit(1)
}
console.log(`[build-backend] OK → ${output}`)
