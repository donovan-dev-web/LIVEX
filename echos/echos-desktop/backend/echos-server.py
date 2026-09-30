"""Point d'entrée PyInstaller du backend ECHOS.

PyInstaller analyse ce script pour découvrir les imports ; il délègue tout à
``echos.server.main`` (aucun code spécifique au packaging).
"""

from echos.server import main

if __name__ == "__main__":
    main()
