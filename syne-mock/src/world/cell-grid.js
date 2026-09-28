// Indexation de `cells[]` dans WorldDescription.
//
// WorldGenerator publie les cellules dans l'ordre y puis x, donc l'index
// lineaire est `y * cellCountX + x`. Toute lecture d'une case doit passer par ici :
// l'inversion `x * cellCountX + y` produit des cles qui se chevauchent des que la
// grille n'est plus carree (cellCountX=20, cellCountY=30 : (0,21) et (1,1) donnent
// la meme cle 21), et faisait considerer comme bloquee une case pourtant franchisee
// sur une carte rectangulaire.
//
// Deux entrees distinctes, car les deux grandeurs ne se confondent pas :
//   - cellIndexAt(world, x, y)  attend une position en unites monde
//   - cellIndexOf(world, cell)  attend les coordonnees de case deja indexees

/** Index lineaire de la case contenant la position monde (x, y). */
function cellIndexAt(world, x, y) {
  return Math.floor(y / world.cellSize) * world.cellCountX + Math.floor(x / world.cellSize);
}

/** Index lineaire d'une case a partir de ses coordonnees de case {x, y}. */
function cellIndexOf(world, cell) {
  return cell.y * world.cellCountX + cell.x;
}

/** Case contenant la position monde (x, y), ou null si la position sort de la carte. */
function cellAt(world, x, y) {
  if (!Number.isFinite(x) || !Number.isFinite(y)) return null;
  if (x < 0 || y < 0 || x >= world.width || y >= world.height) return null;
  const index = cellIndexAt(world, x, y);
  return index >= 0 && index < world.cells.length ? world.cells[index] : null;
}

module.exports = { cellIndexAt, cellIndexOf, cellAt };
