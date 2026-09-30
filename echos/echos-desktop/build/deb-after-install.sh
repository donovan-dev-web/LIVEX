#!/bin/bash
# Post-installation du paquet Debian ECHOS (electron-builder linux.afterInstall,
# exécuté par fpm en root après la copie des fichiers).
#
# Le helper de sandbox Chromium (chrome-sandbox) doit appartenir à root avec le
# bit setuid (4755) : sans lui, Chromium refuse de démarrer sur les noyaux où
# les user namespaces ne suffisent pas — « The SUID sandbox helper binary was
# found, but is not configured correctly. Rather than run without sandboxing
# I'm aborting now ». electron-builder embarque le fichier en 755 ; ce script
# restaure la permission attendue.
#
# L'échec ici ne doit jamais casser l'installation : en dernier recours, l'app
# reste lançable avec --no-sandbox (parade documentée pour le mode dev dans
# scripts/dev-stack-electron.sh).

set +e

CHROME_SANDBOX="/opt/ECHOS/chrome-sandbox"
if [ -f "$CHROME_SANDBOX" ]; then
  chown root:root "$CHROME_SANDBOX"
  chmod 4755 "$CHROME_SANDBOX"
fi

exit 0
