---
title: LIVEX — moteur de simulation sociale
---

<div class="hero">
  <span class="hero-badge">V0.1 · Monorepo dotnet + python + typescript</span>
  <h1>LIVEX — Systems &amp; Emergent Network Engine</h1>
  <p class="hero-tagline">
    Une population d'entités autonomes évolue, et la plateforme <strong>mesure l'émergence</strong> —
    structures, conventions, phénomènes collectifs — sans jamais transformer le phénomène observé.
  </p>
  <p class="hero-description">
    LIVEX couple trois composants : un moteur de simulation déterministe (<strong>SYNE</strong>),
    une plateforme d'analyse en temps réel (<strong>ECHOS</strong>) et un moteur de rendu
    (<strong>PRISM</strong>). Le tout repose sur des contrats publiés, des golden files
    bit-à-bit et une documentation exhaustive.
  </p>
  <div class="hero-cta">
    <a class="btn-lx btn-lx-primary" href="{{_rel}}articles/transverse/README.html">Commencer</a>
    <a class="btn-lx btn-lx-ghost" href="{{_rel}}api/Simulation.Core.html">Référence API</a>
  </div>
</div>

## Les trois composants

<div class="modules">

<a class="lx-card" href="{{_rel}}articles/syne/README.html">
  <h3>SYNE — Simulation.Core</h3>
  <p class="lx-role">Moteur de simulation · <strong>statut : développé (U7)</strong></p>
  <p>Boucle de simulation, entités BDI, perception partielle, décisions, déterminisme
  bit-à-bit. Émet l'état via WebSocket :5180.</p>
  <span class="lx-more">Lire les docs SYNE →</span>
</a>

<a class="lx-card" href="{{_rel}}articles/echos/README.html">
  <h3>ECHOS — analyse</h3>
  <p class="lx-role">Plateforme d'analyse · <strong>statut : développée (U7)</strong></p>
  <p>Stack Python/FastAPI : ingestion en temps réel, agrégation SQLite + Parquet,
  huit moteurs de métriques, indicateurs d'émergence, API REST :5000.</p>
  <span class="lx-more">Lire les docs ECHOS →</span>
</a>

<a class="lx-card" href="{{_rel}}articles/prism/README.html">
  <h3>PRISM — rendu</h3>
  <p class="lx-role">Engine de rendu · <strong>statut : socle (U0)</strong></p>
  <p>Visualisation (Godot) du monde, des croyances et des réseaux observés depuis les
  contrats publiés — l'observation ne transforme jamais le monde simulé.</p>
  <span class="lx-more">Lire les docs PRISM →</span>
</a>

</div>

## Ce qui fait LIVEX

<div class="features">

<div class="lx-feature">
  <h4>Déterminisme strict</h4>
  <p>À seed égale, deux runs produisent exactement la même suite d'événements —
  vérifié bit-à-bit par des golden files.</p>
</div>

<div class="lx-feature">
  <h4>Observation non intrusive</h4>
  <p>ECHOS et PRISM consomment les contrats publiés de SYNE sans jamais écrire
  dans le monde simulé (règle d'or).</p>
</div>

<div class="lx-feature">
  <h4>Métriques d'émergence</h4>
  <p>Huit moteurs purs et déterministes : diversité cognitive, diffusion de
  l'information, complexité sociale, convergence d'objectifs…</p>
</div>

<div class="lx-feature">
  <h4>Scores ≠ preuve</h4>
  <p>Les indicateurs sont tracés, bornés et documentés — jamais présentés comme la
  preuve de l'existence d'une intelligence ou d'une société.</p>
</div>

<div class="lx-feature">
  <h4>Documentation exhaustive</h4>
  <p>Spécifications, ADR, contrats d'ingestion, tests (> 530) et référence API DocFX
  générée depuis les sources.</p>
</div>

<div class="lx-feature">
  <h4>Comparaison expérimentale</h4>
  <p>Reproductibilité bit-à-bit entre runs, distances de croyances/confiance,
  exports JSON/CSV alignés.</p>
</div>

</div>

## Documentation

- [Transversal]({{_rel}}articles/transverse/VISION.html) — vision, architecture, feuille de route, installation.
- [SYNE]({{_rel}}articles/syne/README.html) — moteur de simulation (contrats, config, déterminisme, tests).
- [ECHOS]({{_rel}}articles/echos/README.html) — analyse, agrégation, métriques d'émergence, API REST.
- [PRISM]({{_rel}}articles/prism/README.html) — rendu et scènes.

La **référence API** de `Simulation.Core` est générée par DocFX et accessible depuis
le menu **« API · Simulation.Core »**.

## Principes de gouvernance

- Flux Git : `feature` → `develop` (intégration, CI) → `main` (releases SemVer).
- CI sur chaque PR : build warnaserror, tests, couverture ≥ 80 %, lint.
- Chaque décision d'architecture est tracée dans un **ADR**.

---

*Site généré par [DocFX](https://dotnet.github.io/docfx/) à partir de la
documentation du monorepo LIVEX.*