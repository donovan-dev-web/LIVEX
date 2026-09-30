'use strict'

/**
 * Preload du shell Electron ECHOS.
 *
 * Expose un strict minimum, en contexte isolé : l'interface n'a besoin ni de
 * Node ni des API Electron pour fonctionner (elle parle à l'API via HTTP).
 * Ce pont sert d'accroche pour d'éventuelles fonctions natives (choix de
 * dossier de données, menus) sans ouvrir `nodeIntegration`.
 */

const { contextBridge } = require('electron')

contextBridge.exposeInMainWorld('echosDesktop', {
  platform: process.platform,
  electron: process.versions.electron,
  chrome: process.versions.chrome,
})
