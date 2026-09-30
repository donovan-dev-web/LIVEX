'use strict'

/**
 * Processus principal du shell Electron ECHOS.
 *
 * Il démarre le backend Python (API + interface servie par FastAPI), attend
 * que `/health` réponde, puis ouvre une fenêtre sur l'origine locale — même
 * origine, donc l'interface appelle `/api/*` en relatif sans CORS.
 *
 * Le backend est un enfant du processus principal : il est arrêté à la
 * fermeture de l'application.
 */

const { app, BrowserWindow, dialog, shell } = require('electron')
const { spawn } = require('node:child_process')
const fs = require('node:fs')
const http = require('node:http')
const net = require('node:net')
const path = require('node:path')

let backend = null
let backendPort = null
let mainWindow = null
let quitting = false

/** Demande au noyau un port TCP libre sur la boucle locale. */
function findFreePort() {
  return new Promise((resolve, reject) => {
    const server = net.createServer()
    server.unref()
    server.on('error', reject)
    server.listen(0, '127.0.0.1', () => {
      const { port } = server.address()
      server.close(() => resolve(port))
    })
  })
}

/** Commande et arguments du backend, selon qu'on est packagé ou en dev. */
function backendCommand() {
  if (app.isPackaged) {
    const exe = process.platform === 'win32' ? 'echos-server.exe' : 'echos-server'
    return { command: path.join(process.resourcesPath, 'backend', 'echos-server', exe), args: [] }
  }
  const venv = process.platform === 'win32'
    ? path.join(__dirname, '..', '..', '.venv', 'Scripts', 'python.exe')
    : path.join(__dirname, '..', '..', '.venv', 'bin', 'python')
  return { command: process.env.ECHOS_PYTHON || venv, args: ['-m', 'echos.server'] }
}

function backendEnv(port) {
  const env = {
    ...process.env,
    ECHOS_HOST: '127.0.0.1',
    ECHOS_PORT: String(port),
    // Base d'analyse : l'opérateur peut l'imposer (scripts de stack, CI) —
    // sinon elle vit dans les données utilisateur de l'application
    // (isolation par défaut du mode bureau).
    ECHOS_ANALYTICS_DB:
      process.env.ECHOS_ANALYTICS_DB
      || path.join(app.getPath('userData'), 'echos-analytics.db'),
    PYTHONUNBUFFERED: '1',
  }
  if (app.isPackaged) {
    env.ECHOS_UI_DIST = path.join(process.resourcesPath, 'ui')
  } else {
    // Mode dev : le paquet echos n'est pas installé dans le venv — le rendre
    // importable quel que soit le répertoire depuis lequel le shell est lancé
    // (echos/ contient le paquet ; echos-desktop est son frère).
    const echosDir = path.resolve(__dirname, '..', '..')
    env.PYTHONPATH = env.PYTHONPATH
      ? `${echosDir}${path.delimiter}${env.PYTHONPATH}`
      : echosDir
  }
  return env
}

/** Attend que `GET /health` réponde 200 (démarrage du backend). */
function waitForHealth(port, timeoutMs = 40000) {
  const deadline = Date.now() + timeoutMs
  return new Promise((resolve, reject) => {
    const attempt = () => {
      const req = http.get({ host: '127.0.0.1', port, path: '/health', timeout: 2000 }, (res) => {
        res.resume()
        if (res.statusCode === 200) return resolve()
        retry()
      })
      req.on('error', retry)
      req.on('timeout', () => {
        req.destroy()
        retry()
      })
    }
    const retry = () => {
      if (Date.now() > deadline) return reject(new Error('le backend ECHOS ne répond pas (/health)'))
      setTimeout(attempt, 250)
    }
    attempt()
  })
}

async function startBackend() {
  backendPort = await findFreePort()
  const { command, args } = backendCommand()
  if (!fs.existsSync(command)) {
    throw new Error(`backend introuvable : ${command}\nLancez « npm run build:backend » ou vérifiez .venv.`)
  }
  backend = spawn(command, args, {
    env: backendEnv(backendPort),
    stdio: ['ignore', 'pipe', 'pipe'],
    windowsHide: true,
  })
  backend.stdout.on('data', (chunk) => process.stdout.write(`[echos] ${chunk}`))
  backend.stderr.on('data', (chunk) => process.stderr.write(`[echos] ${chunk}`))
  backend.on('exit', (code, signal) => {
    backend = null
    if (!quitting) {
      dialog.showErrorBox('ECHOS', `Le backend s'est arrêté de façon inattendue (code ${code}, signal ${signal}).`)
      app.quit()
    }
  })
  await waitForHealth(backendPort)
}

function stopBackend() {
  if (!backend) return
  const child = backend
  backend = null
  try {
    child.kill('SIGTERM')
  } catch {
    /* déjà terminé */
  }
  const killer = setTimeout(() => {
    try {
      child.kill('SIGKILL')
    } catch {
      /* déjà terminé */
    }
  }, 3000)
  killer.unref()
}

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 860,
    minWidth: 960,
    minHeight: 640,
    title: 'ECHOS',
    backgroundColor: '#0b0f14',
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  })
  mainWindow.once('ready-to-show', () => mainWindow.show())
  mainWindow.on('closed', () => {
    mainWindow = null
  })
  mainWindow.webContents.setWindowOpenHandler(({ url }) => {
    shell.openExternal(url)
    return { action: 'deny' }
  })

  const devUrl = process.env.ECHOS_DEV_URL
  mainWindow.loadURL(devUrl || `http://127.0.0.1:${backendPort}/`)
}

const gotLock = app.requestSingleInstanceLock()
if (!gotLock) {
  app.quit()
} else {
  app.on('second-instance', () => {
    if (mainWindow) {
      if (mainWindow.isMinimized()) mainWindow.restore()
      mainWindow.focus()
    }
  })

  app.whenReady().then(async () => {
    try {
      await startBackend()
      createWindow()
    } catch (err) {
      dialog.showErrorBox('ECHOS — démarrage impossible', String((err && err.message) || err))
      app.quit()
    }
    app.on('activate', () => {
      if (BrowserWindow.getAllWindows().length === 0) createWindow()
    })
  })
}

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})

app.on('before-quit', () => {
  quitting = true
  stopBackend()
})
