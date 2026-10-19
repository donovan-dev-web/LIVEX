# PRISM — acceptation de la phase 5 (octobre 2026)

**Date du relevé :** 17 octobre 2026 · **Volet :** BL-25 · **Portée :** état autoritaire reçu, rejeu d’un flux troué, absence d’extrapolation.

## Critères et résultats

Le chemin d’état du plugin PRISM-LDK a été vérifié : un snapshot remplace l’état publié; le dernier état autoritaire est le dernier snapshot reçu; aucune interpolation ou extrapolation ne s’insère dans le chemin d’état C++. Les écritures vers SYNE sont des commandes explicites de contrôle protégées, distinctes du rendu.

Un rejeu de ticks 41 puis 47 (trous 42–46) a été ajouté à deux Automation Specs. Le dernier état reçu demeure l’état courant pendant le silence; aucun tick manquant n’est fabriqué. À la déconnexion, l’état reçu reste consultable et le flux est déclaré dégradé. Le relevé sur Unreal Engine 5.8 indique **63 Automation Specs réussies, 0 échec** (exit 0), dont les deux specs `PrismLdk.GapReplay`.

## Réserve de fraîcheur

La détection d’un flux déconnecté est couverte; un WebSocket connecté mais silencieux n’est pas distingué d’un flux actif, et aucune garde de monotonie de tick/champ de staleness n’est publiée. Ce n’était pas prescrit par le contrat `TRANSPORT_API.md` examiné lors de l’acceptation; aucune modification de production n’a donc été engagée. Si un consommateur a besoin d’une détection de flux figé, il faudra définir le contrat et ses tests avant d’ajouter cette capacité.

L’interpolation éventuelle du rendu visuel entre deux snapshots reçus est distincte du chemin d’état autoritaire et n’est pas réinjectée dans SYNE. Le mock et les specs Unreal vérifient l’intégration; ils ne remplacent pas une campagne scientifique du moteur.
