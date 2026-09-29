/**
 * Déclenche le téléchargement d'un contenu texte déjà normalisé.
 *
 * Le service ECHOS renvoie toujours du JSON, y compris pour les exports CSV
 * (le CSV est transporté dans une chaîne `body`). La conversion en fichier
 * téléchargeable est donc faite ici, et non par l'appelant.
 */
export function triggerDownload(filename: string, mime: string, content: string): void {
  const url = URL.createObjectURL(new Blob([content], { type: `${mime};charset=utf-8` }))
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(url)
}
