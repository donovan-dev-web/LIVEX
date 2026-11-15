#!/usr/bin/env python3
# SPDX-FileCopyrightText: 2026 Donovan Chartrain
# SPDX-License-Identifier: GPL-3.0-or-later
"""Run R3 de la porte G5 : 100 agents x 5 000 ticks, deux passes reproductibles.

Une commande, un JSON versionne et un brouillon de rapport :

    python scripts/prism-g5-r3.py

Le script ne mesure pas la performance du tick (c'est le role de
`prism-coupled-benchmark.py`, PRISM-106) : il produit le run de reference de G5
et verifie ce que la porte demande reellement, a savoir deux passes completes
(100 agents x 5 000 ticks, production par les agents SYNE) qui rendent la meme
empreinte. Il refuse de publier un resultat partiel.

Options utiles :
    --no-build          binaires Release deja construits
    --runs 2            nombre de passes (la porte en demande deux)
    --dry-run           affiche les commandes sans rien lancer
    --keep-work         conserve les repertoires de travail et les journaux
    --kill-host         tue les prism-host.exe restants avant la build

Sortie :
    docs/prism/benchmarks/G5-R3-<date>-v1.json
    docs/prism/G5-R3-REPORT-<date>.md   (brouillon a completer)

Codes de sortie : 0 = R3 produite et reproductible ; 1 = echec ; 2 = empreintes
divergentes ou run incomplet ; 3 = delai depasse.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import socket
import statistics
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timezone
from pathlib import Path

SCHEMA = "livex.prism.g5-r3.v1"
DEFAULT_SEED = 7
DEFAULT_AGENTS = 100
DEFAULT_TICKS = 5000
DEFAULT_CONTENT_HASH = (
    "793A8996297CF2562ABB112F638995BAD8D40CD843CE8CA4608712257F04AA82"
)
DEFAULT_CONTENT_DIR = "prism/content/minimal"
DEFAULT_PRODUCTION = "stone_pickaxe,chest,wooden_bucket,stone_hoe,furnace"
DEFAULT_TOKEN = "g5-r3-livex"


def repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


def log(message: str) -> None:
    print(message, flush=True)


def free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("127.0.0.1", 0))
        return int(sock.getsockname()[1])


def high_priority_flags() -> int:
    if sys.platform != "win32":
        return 0
    return 0x00000080 | 0x08000000  # HIGH_PRIORITY_CLASS | CREATE_NO_WINDOW


def run_command(command: list[str], cwd: Path, env: dict, timeout_s: float,
                encoding: str = "utf-8") -> subprocess.CompletedProcess:
    # La console .NET ecrit en page de code Windows quand sa sortie est
    # redirigee : decoder en UTF-8 casserait les accents des lignes de synthese.
    return subprocess.run(
        command,
        cwd=str(cwd),
        env=env,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=timeout_s,
        text=True,
        encoding=encoding,
        errors="replace",
        creationflags=high_priority_flags(),
    )


def host_dll(repo: Path) -> Path:
    return repo / "prism/Prism.Host/bin/Release/net10.0/prism-host.dll"


def client_dll(repo: Path) -> Path:
    return repo / "syne/Simulation.Console/bin/Release/net10.0/Simulation.Console.dll"


def running_hosts() -> list[str]:
    """Processus prism-host.exe visibles (une build echoue si l'hote tourne)."""
    if sys.platform != "win32":
        result = subprocess.run(["pgrep", "-fl", "prism-host"], stdout=subprocess.PIPE,
                                stderr=subprocess.DEVNULL, text=True)
        return [line for line in result.stdout.splitlines() if line.strip()]
    result = subprocess.run(
        ["tasklist", "/FI", "IMAGENAME eq prism-host.exe", "/NH"],
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
        encoding="cp1252",
        errors="replace",
    )
    return [line for line in result.stdout.splitlines() if "prism-host.exe" in line.lower()]


def build(repo: Path, kill_host: bool) -> None:
    hosts = running_hosts()
    if hosts:
        if not kill_host:
            raise SystemExit(
                "prism-host.exe tourne encore : une build Release echouerait sur les "
                "binaires verrouilles.\nArrete-le, ou relance avec --kill-host.\n"
                + "\n".join(hosts)
            )
        log("arret des prism-host.exe restants (--kill-host)")
        subprocess.run(["taskkill", "/F", "/IM", "prism-host.exe"],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    for project in (
        "prism/Prism.Host/Prism.Host.csproj",
        "syne/Simulation.Console/Simulation.Console.csproj",
    ):
        log(f"build {project}")
        result = run_command(
            ["dotnet", "build", project, "-c", "Release", "--nologo", "-v", "q"],
            repo,
            os.environ.copy(),
            timeout_s=600.0,
        )
        if result.returncode != 0:
            raise SystemExit(
                f"Echec de la build de {project} :\n{result.stdout}\n{result.stderr}".strip()
            )


def http_ready(url: str, timeout_s: float) -> bool:
    deadline = time.monotonic() + timeout_s
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen(url, timeout=1.0) as response:
                if response.status == 200:
                    return True
        except (urllib.error.URLError, ConnectionError, TimeoutError, OSError):
            pass
        time.sleep(0.25)
    return False


def post_shutdown(url: str, token: str) -> None:
    request = urllib.request.Request(
        url,
        data=b"{}",
        headers={"Content-Type": "application/json", "X-LIVEX-Session-Token": token},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=10.0) as response:
        response.read()


def git_provenance(repo: Path) -> dict:
    def git(*args: str) -> str:
        result = subprocess.run(["git", *args], cwd=str(repo), stdout=subprocess.PIPE,
                                stderr=subprocess.DEVNULL, text=True)
        return result.stdout.strip() if result.returncode == 0 else ""

    return {
        "gitCommit": git("rev-parse", "--short", "HEAD"),
        "gitBranch": git("rev-parse", "--abbrev-ref", "HEAD"),
        "workingTreeDirty": bool(git("status", "--porcelain")),
    }


def dotnet_version(repo: Path) -> str:
    result = subprocess.run(["dotnet", "--version"], cwd=str(repo), stdout=subprocess.PIPE,
                            stderr=subprocess.DEVNULL, text=True)
    return result.stdout.strip() if result.returncode == 0 else ""


def parse_client_output(text: str) -> dict:
    """Extrait les lignes de synthese du client couple (aucune valeur inventee)."""
    fingerprint = re.search(r"empreinte[^\n:]*:\s*([0-9a-f]{8,})", text)
    completed = re.search(r"(\d+)/(\d+)\s+ticks OK", text)
    effect = re.search(r"couplage effectif\s*:\s*(\d+)[^\n]*?positionn", text)
    moved = re.search(r"couplage effectif\s*:[^\n]*?(\d+)\s+d\S*placement", text)
    minds = re.search(r"couplage effectif\s*:[^\n]*?(\d+)/(\d+)\s+esprit", text)
    commands = re.search(r"(\d+)\s+commande\(s\)\s+\S*mise", text)
    production = re.search(
        r"production\s*:\s*(\d+)\s+cha\S*ne\(s\)\s+termin\S*e\(s\)"
        r"(?:\s*\[([^\]]*)\])?,\s*(\d+)\s+abandon",
        text,
    )
    writeback = re.search(r"write-back\s*:\s*(.+)", text)
    issues = re.search(r"issues PRISM\s*:\s*(.+?)\s*;\s*refus durables", text)
    unsupported = re.search(r"objectifs sans recette\s*:\s*(.+)$", text, re.M)

    return {
        "fingerprint": fingerprint.group(1) if fingerprint else None,
        "completedTicks": int(completed.group(1)) if completed else None,
        "expectedTicks": int(completed.group(2)) if completed else None,
        "observations": int(effect.group(1)) if effect else None,
        "moves": int(moved.group(1)) if moved else None,
        "minds": int(minds.group(1)) if minds else None,
        "entities": int(minds.group(2)) if minds else None,
        "commandsSent": int(commands.group(1)) if commands else None,
        "chainsCompleted": int(production.group(1)) if production else None,
        "chains": [item.strip() for item in production.group(2).split(",") if item.strip()]
        if production and production.group(2) else [],
        "abandonedRuns": int(production.group(3)) if production else None,
        "unsupportedGoals": [item.strip() for item in unsupported.group(1).split(",") if item.strip()]
        if unsupported else [],
        "writebackLine": writeback.group(1).strip() if writeback else None,
        "issues": issues.group(1).strip() if issues else None,
    }


def run_pass(repo: Path, index: int, args: argparse.Namespace, pass_dir: Path,
             env_base: dict) -> dict:
    port = free_port()
    token = args.token
    if args.dry_run:
        host_command = ["dotnet", str(host_dll(repo)), "--control-port", str(port),
                        "--work-dir", str(pass_dir / "work"), "--instance-id", f"r3-p{index}",
                        "--log-dir", str(pass_dir / "logs"), "--correlation-id", f"prism-g5-r3-p{index}"]
        client_command = ["dotnet", str(client_dll(repo)), "--prism-url", f"http://127.0.0.1:{port}",
                          "--prism-agents", str(args.agents), "--prism-ticks", str(args.ticks),
                          "--prism-seed", str(args.seed), "--prism-content-hash", args.content_hash,
                          "--prism-content-dir", str((repo / args.content_dir).resolve()),
                          "--production", args.production]
        log(f"  passe {index} : hote :{port}")
        log("    " + " ".join(host_command))
        log("    " + " ".join(client_command))
        return {"pass": index, "dryRun": True, "port": port,
                "hostCommand": host_command, "clientCommand": client_command}

    pass_dir.mkdir(parents=True, exist_ok=True)
    host_out = (pass_dir / "host.out").open("w", encoding="utf-8")
    host_err = (pass_dir / "host.err").open("w", encoding="utf-8")

    env = env_base.copy()
    env["LIVEX_SESSION_TOKEN"] = token
    # Le checkpoint d'arret n'est pas un cout de tick et n'entre pas dans
    # l'empreinte ; a 100 agents il ajoute plusieurs secondes au dernier tick.
    env["PRISM_CHECKPOINT_ON_STOP"] = "0"
    # Profil client : il separe le cout propre a SYNE (deliberation, application
    # du write-back) du tick de l'hote. Le profil est documente comme neutre sur
    # l'empreinte (PrismCoupledProfile.cs).
    profile_json = pass_dir / "client-profile.json"
    env["SYNE_COUPLED_PROFILE"] = "1"
    env["SYNE_COUPLED_PROFILE_JSON"] = str(profile_json)

    host_command = [
        "dotnet",
        str(host_dll(repo)),
        "--control-port", str(port),
        "--work-dir", str(pass_dir / "work"),
        "--instance-id", f"r3-p{index}",
        "--log-dir", str(pass_dir / "logs"),
        "--correlation-id", f"prism-g5-r3-p{index}",
    ]
    client_command = [
        "dotnet",
        str(client_dll(repo)),
        "--prism-url", f"http://127.0.0.1:{port}",
        "--prism-agents", str(args.agents),
        "--prism-ticks", str(args.ticks),
        "--prism-seed", str(args.seed),
        "--prism-content-hash", args.content_hash,
        "--prism-content-dir", str((repo / args.content_dir).resolve()),
        "--production", args.production,
    ]

    log(f"  passe {index} : hote :{port}, {args.agents} agents x {args.ticks} ticks")
    host = subprocess.Popen(host_command, cwd=str(repo), env=env,
                            stdout=host_out, stderr=host_err,
                            creationflags=high_priority_flags())
    client_started = time.monotonic()
    try:
        if not http_ready(f"http://127.0.0.1:{port}/health/live", args.startup_timeout):
            raise RuntimeError("l'hote PRISM n'a pas repondu a /health/live")

        log(f"  passe {index} : client couple lance (delai max {args.timeout:.0f} s)")
        client = run_command(client_command, repo, env, timeout_s=args.timeout,
                             encoding="cp1252")
        elapsed = time.monotonic() - client_started
        (pass_dir / "client.out").write_text(client.stdout, encoding="utf-8")
        (pass_dir / "client.err").write_text(client.stderr, encoding="utf-8")
        parsed = parse_client_output(client.stdout)
        profile = {}
        if profile_json.exists():
            try:
                payload = json.loads(profile_json.read_text(encoding="utf-8"))
                ticks = [item for item in payload.get("ticks", []) if item]
                if ticks:
                    profile = {
                        "decideMs": round(statistics.median(float(item["decideMs"]) for item in ticks), 1),
                        "applyMs": round(statistics.median(float(item["applyMs"]) for item in ticks), 1),
                        "prismMs": round(statistics.median(float(item["prismMs"]) for item in ticks), 1),
                        "measuredTicks": len(ticks),
                    }
                    profile["syneMsPerTick"] = round(profile["decideMs"] + profile["applyMs"], 1)
            except (OSError, ValueError, KeyError):
                profile = {}
        result = {
            "pass": index,
            "port": port,
            "exitCode": client.returncode,
            "durationSeconds": round(elapsed, 1),
            "msPerTick": round(elapsed * 1000.0 / args.ticks, 1),
            "hostCommand": host_command,
            "clientCommand": client_command,
            "runDir": str(pass_dir),
            "clientProfile": profile,
            **parsed,
        }
        if client.returncode != 0:
            result["failure"] = (client.stdout[-2000:] + client.stderr[-2000:]).strip()
        return result
    finally:
        try:
            post_shutdown(f"http://127.0.0.1:{port}/control/shutdown", token)
        except Exception:
            pass
        try:
            host.wait(timeout=15)
        except subprocess.TimeoutExpired:
            host.terminate()
            try:
                host.wait(timeout=10)
            except subprocess.TimeoutExpired:
                host.kill()
        host_out.close()
        host_err.close()


def verdict(passes: list[dict], args: argparse.Namespace) -> dict:
    fingerprints = [item.get("fingerprint") for item in passes]
    complete = all(
        item.get("completedTicks") == args.ticks and item.get("expectedTicks") == args.ticks
        and item.get("exitCode") == 0
        for item in passes
    )
    identical = all(fingerprints) and len(set(fingerprints)) == 1
    incarnated = all(
        (item.get("minds") or 0) == args.agents and (item.get("moves") or 0) > 0
        for item in passes
    )
    return {
        "fingerprints": fingerprints,
        "identical": identical,
        "complete": complete,
        "incarnated": incarnated,
        "produced": bool(identical and complete and incarnated),
    }


def markdown_report(date: str, args: argparse.Namespace, passes: list[dict],
                    decision: dict, provenance: dict) -> str:
    produced = decision["produced"]
    status = (
        f"**R3 produite et reproductible** — {len(passes)} passes completes, "
        f"empreinte `{decision['fingerprints'][0]}`"
        if produced
        else "**R3 non concluante** — voir le detail des passes ci-dessous"
    )
    rows = []
    for item in passes:
        rows.append(
            "| {pass} | {duration} s | {mspt} ms | {done}/{expected} | `{fp}` | "
            "{obs} | {moves} | {minds}/{entities} | {chains} | {abandon} |".format(
                **{
                    "pass": item.get("pass"),
                    "duration": item.get("durationSeconds"),
                    "mspt": item.get("msPerTick"),
                    "done": item.get("completedTicks"),
                    "expected": item.get("expectedTicks"),
                    "fp": item.get("fingerprint") or "—",
                    "obs": item.get("observations") if item.get("observations") is not None else "—",
                    "moves": item.get("moves") if item.get("moves") is not None else "—",
                    "minds": item.get("minds") if item.get("minds") is not None else "—",
                    "entities": item.get("entities") if item.get("entities") is not None else "—",
                    "chains": item.get("chainsCompleted") if item.get("chainsCompleted") is not None else "—",
                    "abandon": item.get("abandonedRuns") if item.get("abandonedRuns") is not None else "—",
                }
            )
        )
    writeback = "\n".join(
        f"- passe {item['pass']} — `{item.get('writebackLine') or 'ligne absente'}`"
        for item in passes
    )
    commands = "\n".join(
        f"```bash\n# passe {item['pass']}\n"
        + " ".join(item.get("hostCommand") or [])
        + "\n"
        + " ".join(item.get("clientCommand") or [])
        + "\n```"
        for item in passes
    )
    return f"""# Rapport de porte G5 — run R3 ({date})

**Composant** : SYNE (cognition) couplé à l'hôte PRISM
**Porte** : G5 — « SYNE est-il allégé et capable ? »
**Run** : R3 — {args.agents} agents × {args.ticks} ticks, {len(passes)} passes, production par les agents SYNE
**Statut** : {status}
**Matériel / build** : {provenance.get('platform')}, `Release`, pack minimal `256 × 256`, contenu `{args.content_hash}`

> Brouillon généré par `scripts/prism-g5-r3.py` le {date}. Les tableaux et les empreintes sont
> issus des journaux de run ; les sections marquées « À RÉDIGER » demandent une lecture humaine.

---

## 1. Ce que la porte demande

| Critère | Valeur mesurée | Verdict |
| :-- | :-- | :-- |
| `Simulation.Core` sans classe de monde | `PrismWorldAdapter` seule implémentation de `IWorld` | ✅ (établi le 7 novembre 2026) |
| Agents SYNE accomplissent ≥ 3 chaînes sans plan scripté | {max((item.get('chainsCompleted') or 0) for item in passes)} chaîne(s) terminée(s) sur la dernière passe | {'✅' if max((item.get('chainsCompleted') or 0) for item in passes) >= 3 else '⛔'} |
| **R3 reproductible** | {len(passes)} passes, empreintes {'identiques' if decision['identical'] else 'divergentes'} `{', '.join(str(value) for value in decision['fingerprints'])}` | {'✅' if decision['produced'] else '⛔'} |
| Budget SYNE tenu (50 ms/tick à 100 agents) | coût propre à SYNE **{passes[0].get('clientProfile', {}).get('syneMsPerTick', '—')} ms/tick** (délibération + application), aller-retour {passes[0].get('clientProfile', {}).get('prismMs', '—')} ms hôte compris | {'✅' if isinstance(passes[0].get('clientProfile', {}).get('syneMsPerTick'), (int, float)) and passes[0]['clientProfile']['syneMsPerTick'] < 50 else '⛔'} voir §7 |

## 2. Méthode

Chaque passe part d'un hôte neuf (`--work-dir` distinct), le client couple incarne
{args.agents} agents SYNE, leur transmet les observations PRISM et traduit leurs décisions en
commandes du contrat. Le checkpoint d'arrêt est désactivé (`PRISM_CHECKPOINT_ON_STOP=0`) : il
n'entre ni dans l'empreinte ni dans le comportement, et sa sérialisation dominerait le dernier tick.

{commands}

## 3. Passes

| Passe | Durée | Coût/tick | Ticks terminés | Empreinte | Observations | Déplacements | Esprits | Chaînes | Abandons |
| --: | --: | --: | --: | :-- | --: | --: | --: | --: | --: |
{chr(10).join(rows)}

## 4. Reproductibilité

- Empreintes : `{', '.join(str(value) for value in decision['fingerprints'])}`
- Identiques : **{'oui' if decision['identical'] else 'non'}**
- Runs complets : **{'oui' if decision['complete'] else 'non'}**
- Incarnation effective : **{'oui' if decision['incarnated'] else 'non'}** ({args.agents}/{args.agents} esprits et déplacements > 0 attendus)

<!-- À RÉDIGER : ce que la comparaison des deux passes prouve, et ce qu'elle ne prouve pas
     (même hôte, même binaire, même graine ; l'interplateforme est hors périmètre — PRISM-079). -->

## 5. Write-back observé

{writeback}

<!-- À RÉDIGER : lecture des issues (source_not_found, path_not_found, action_already_active),
     chaînes terminées et abandons, cohérence avec la forme réduite `233d76e194c897cf`. -->

## 6. Coût observé

{chr(10).join(f"- passe {item['pass']} : {item.get('durationSeconds')} s pour {item.get('completedTicks')} ticks, soit **{item.get('msPerTick')} ms/tick** (hôte + transport + cognition)." for item in passes)}

| Passe | Délibération SYNE | Application du write-back | Coût SYNE | Aller-retour |
| --: | --: | --: | --: | --: |
{chr(10).join(f"| {item['pass']} | {item.get('clientProfile', {}).get('decideMs', '—')} ms | {item.get('clientProfile', {}).get('applyMs', '—')} ms | **{item.get('clientProfile', {}).get('syneMsPerTick', '—')} ms** | {item.get('clientProfile', {}).get('prismMs', '—')} ms |" for item in passes)}

<!-- À RÉDIGER : comparer au relevé PRISM-106 du 14 novembre 2026 (aller-retour 367,8 ms,
     décision 6,3 ms, application 17,3 ms à 100 agents) et nommer l'écart s'il dépasse
     l'erreur-type. Le tick de l'hôte (355 ms) relève de G1, pas de la cognition. -->

## 7. Ce qui reste pour clore G5

Le critère « R3 reproductible » est {'atteint' if decision['produced'] else 'non atteint'}. Le critère
« budget SYNE tenu » se lit sur le **coût propre à SYNE** ({passes[0].get('clientProfile', {}).get('syneMsPerTick', '—')} ms/tick
mesurés ici), pas sur l'aller-retour : l'hôte PRISM pèse {passes[0].get('clientProfile', {}).get('prismMs', '—')} ms à lui seul et relève du budget
de G1 (`PERFORMANCE.md` §2), pas de la cognition.

<!-- À RÉDIGER — décision de porte, une des deux seulement :
     (a) G5 franchie pour la capacité, budget reporté sur G1 avec une ADR de budget citée ;
     (b) G5 maintenue ouverte jusqu'à ce que G1 tienne les 50 ms. -->

## 8. Provenance

| Champ | Valeur |
| :-- | :-- |
| Commit | `{provenance.get('gitCommit')}` sur `{provenance.get('gitBranch')}` |
| Arbre de travail modifié | {'oui' if provenance.get('workingTreeDirty') else 'non'} |
| .NET | `{provenance.get('dotnet')}` |
| Plateforme | `{provenance.get('platform')}` |
| Python | `{provenance.get('python')}` |
| Relevé machine | `docs/prism/benchmarks/G5-R3-{date}-v1.json` |
"""


def write_outputs(repo: Path, date: str, args: argparse.Namespace, passes: list[dict],
                  decision: dict, provenance: dict) -> tuple[Path, Path]:
    json_path = Path(args.out) if args.out else (
        repo / "docs/prism/benchmarks" / f"G5-R3-{date}-v1.json"
    )
    report_path = Path(args.report) if args.report else (
        repo / "docs/prism" / f"G5-R3-REPORT-{date}.md"
    )
    payload = {
        "schema": SCHEMA,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
        "seed": args.seed,
        "agents": args.agents,
        "ticks": args.ticks,
        "contentHash": args.content_hash,
        "contentDir": args.content_dir,
        "production": args.production.split(","),
        "checkpointOnStop": False,
        "passes": passes,
        "verdict": decision,
        "provenance": provenance,
        "reportDraft": str(report_path),
    }
    json_path.parent.mkdir(parents=True, exist_ok=True)
    json_path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(
        markdown_report(date, args, passes, decision, provenance), encoding="utf-8"
    )
    return json_path, report_path


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Run R3 de la porte G5 (100 agents x 5 000 ticks, deux passes)."
    )
    parser.add_argument("--agents", type=int, default=DEFAULT_AGENTS)
    parser.add_argument("--ticks", type=int, default=DEFAULT_TICKS)
    parser.add_argument("--runs", type=int, default=2, help="passes (defaut : 2, la porte en exige 2)")
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    parser.add_argument("--content-hash", default=DEFAULT_CONTENT_HASH)
    parser.add_argument("--content-dir", default=DEFAULT_CONTENT_DIR)
    parser.add_argument("--production", default=DEFAULT_PRODUCTION)
    parser.add_argument("--token", default=DEFAULT_TOKEN)
    parser.add_argument("--timeout", type=float, default=5400.0, help="delai max par passe, en secondes")
    parser.add_argument("--startup-timeout", type=float, default=180.0)
    parser.add_argument("--out", default=None, help="chemin du JSON de releve")
    parser.add_argument("--report", default=None, help="chemin du brouillon de rapport")
    parser.add_argument("--no-build", action="store_true", help="ne pas construire les binaires")
    parser.add_argument("--kill-host", action="store_true", help="tuer prism-host.exe avant la build")
    parser.add_argument("--keep-work", action="store_true", help="conserver les repertoires de travail")
    parser.add_argument("--dry-run", action="store_true", help="afficher les commandes sans lancer")
    args = parser.parse_args()

    repo = repo_root()
    date = datetime.now().strftime("%Y-%m-%d")
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    work_root = repo / "prism/build/g5-r3" / stamp

    log(
        f"G5 / R3 : {args.runs} passe(s) de {args.agents} agents x {args.ticks} ticks, "
        f"graine {args.seed}"
    )
    log(f"contenu {args.content_hash[:16]}..., production {args.production}")
    log(f"travail : {work_root}")

    if not args.dry_run:
        if not args.no_build:
            build(repo, args.kill_host)
        if not host_dll(repo).exists() or not client_dll(repo).exists():
            raise SystemExit(
                "Binaires Release absents : relancer sans --no-build, ou construire "
                "prism/Prism.Host et syne/Simulation.Console en Release."
            )

    passes: list[dict] = []
    env_base = os.environ.copy()
    env_base["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    env_base.pop("PRISM_BENCHMARK_JSON", None)  # pas de JSONL : R3 n'est pas un releve de perf

    started = time.monotonic()
    try:
        for index in range(1, args.runs + 1):
            pass_dir = work_root / f"pass{index}"
            try:
                result = run_pass(repo, index, args, pass_dir, env_base)
            except subprocess.TimeoutExpired:
                log(f"ECHEC : passe {index} au-dela de {args.timeout:.0f} s")
                return 3
            except RuntimeError as error:
                log(f"ECHEC : passe {index} — {error}")
                return 1
            passes.append(result)
            if args.dry_run:
                continue
            log(
                f"  passe {index} : {result.get('durationSeconds')} s, "
                f"{result.get('completedTicks')}/{result.get('expectedTicks')} ticks, "
                f"empreinte {result.get('fingerprint')}, "
                f"{result.get('chainsCompleted')} chaîne(s), "
                f"{result.get('abandonedRuns')} abandon(s)"
            )
    finally:
        if not args.dry_run and not args.keep_work:
            import shutil
            shutil.rmtree(work_root, ignore_errors=True)

    if args.dry_run:
        log("mode --dry-run : aucune passe executee, aucun fichier ecrit.")
        return 0

    provenance = {
        **git_provenance(repo),
        "dotnet": dotnet_version(repo),
        "platform": sys.platform,
        "python": sys.version.split()[0],
        "cpus": os.cpu_count(),
    }
    decision = verdict(passes, args)
    json_path, report_path = write_outputs(repo, date, args, passes, decision, provenance)

    log("")
    for item in passes:
        log(
            f"passe {item['pass']} : empreinte {item.get('fingerprint')} | "
            f"{item.get('completedTicks')}/{item.get('expectedTicks')} ticks | "
            f"{item.get('msPerTick')} ms/tick | {item.get('chainsCompleted')} chaîne(s)"
        )
    log(f"reproductible : {'OUI' if decision['identical'] else 'NON'} | "
        f"complet : {'OUI' if decision['complete'] else 'NON'} | "
        f"incarnation : {'OUI' if decision['incarnated'] else 'NON'}")
    log(f"duree totale : {round(time.monotonic() - started, 1)} s")
    log(f"JSON : {json_path}")
    log(f"brouillon de rapport : {report_path}")

    if not decision["produced"]:
        log("ECHEC : R3 non concluante — les journaux de run sont conserves sous "
            f"{work_root}" if args.keep_work else
            "ECHEC : R3 non concluante — relancer avec --keep-work pour garder les journaux.")
        return 2
    log("OK : R3 produite, deux passes completes et de meme empreinte.")
    log("Il reste a completer les sections « À RÉDIGER » du brouillon et a statuer sur le budget (§7).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
