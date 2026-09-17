# Contribution et règles de développement

## Principes

1. Le Simulation Core ne dépend d'aucun renderer.
2. Les comportements globaux ne doivent pas être codés directement.
3. Les actions décrivent des capacités, pas des scénarios.
4. Les décisions doivent être observables.
5. Les paramètres expérimentaux doivent être configurables.
6. Toute optimisation importante doit être justifiée par un benchmark.
7. Les changements du modèle de simulation doivent être documentés.

## Avant d'ajouter un comportement

Se demander :

- Est-ce une capacité ?
- Est-ce une règle biologique ?
- Est-ce une perception ?
- Est-ce une règle de décision ?
- Est-ce une conséquence du monde ?
- Est-ce un scénario caché ?

Si la réponse est « scénario caché », le changement doit être réévalué.
