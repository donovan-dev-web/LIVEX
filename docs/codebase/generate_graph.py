#!/usr/bin/env python3
"""Build the local, static graph data used by the LIVEX codebase explorer."""

from __future__ import annotations

import ast
import hashlib
import json
import re
import subprocess
from datetime import datetime
from pathlib import Path, PurePosixPath
from urllib.parse import unquote


ROOT = Path(__file__).resolve().parents[2]
OUTPUT = Path(__file__).resolve().parent / "graph-data.js"
SKIP_PREFIX = "docs/codebase/"
TEXT_EXTENSIONS = {
    ".astro", ".axaml", ".c", ".cff", ".cpp", ".cs", ".cshtml", ".css",
    ".csproj", ".editorconfig", ".gitignore", ".h", ".hpp", ".html", ".ini",
    ".js", ".json", ".md", ".mjs", ".props", ".py", ".pyi", ".sh", ".toml",
    ".ts", ".tsx", ".txt", ".xml", ".yaml", ".yml",
}
CODE_EXTENSIONS = {
    ".astro", ".c", ".cpp", ".cs", ".h", ".hpp", ".js", ".mjs", ".py",
    ".pyi", ".ts", ".tsx",
}
DOCUMENT_EXTENSIONS = {".md", ".rst", ".txt"}
IGNORED_PATH_PARTS = {
    ".git", ".venv", "bin", "dist", "node_modules", "obj", "__pycache__",
}

COMPONENTS = {
    "syne": ("SYNE", "Moteur de simulation déterministe en C#/.NET."),
    "echos": ("ECHOS", "Ingestion, analyse et API REST headless en Python."),
    "prism": ("PRISM", "Projet hôte Unreal et plugin PRISM-LDK."),
    "launcher": ("Launcher", "Orchestration .NET/Avalonia de la pile LIVEX."),
    "syne-mock": ("syne-mock", "Serveur Node.js de contrats simulés pour l'intégration."),
    "site": ("Site web", "Site documentaire et vitrine Astro."),
    "scripts": ("Scripts", "Outils d'expérimentation, de campagne et de benchmark."),
    "configs": ("Configurations", "Configurations de simulation."),
    "docs": ("Documentation", "Documentation d'architecture, contrats, ADR et monographie."),
    ".github": ("GitHub", "Automatisation CI/CD et modèles de contribution."),
}
ROOT_COMPONENT = ("Racine", "Métadonnées, gouvernance et documentation de premier niveau.")
ARCHITECTURE_FLOWS = [
    ("syne", "echos", "WebSocket :5180 · snapshots et événements", "contract"),
    ("syne", "prism", "WebSocket :5180 · snapshots et événements", "contract"),
    ("syne", "prism", "HTTP :5181 · commandes de contrôle", "contract"),
    ("echos", "syne", "HTTP relayé · contrôle de simulation", "contract"),
    ("echos", "launcher", "HTTP REST :5000 · runs, métriques et rapports", "contract"),
    ("launcher", "syne", "Orchestration · processus et contrats versionnés", "integration"),
    ("launcher", "echos", "Analyse headless · API et rapports", "integration"),
    ("syne-mock", "prism", "Contrats simulés · tests d'intégration PRISM", "testing"),
]

CLASS_RE = re.compile(
    r"^\s*(?:(?:public|private|protected|internal|export|abstract|sealed|static|partial|readonly|file)\s+)*"
    r"(?:class|interface|enum|struct|record|type)\s+(?P<name>[A-Za-z_$][\w$]*)"
    r"(?:\s*\((?P<args>[^()\n]*)\))?",
    re.MULTILINE,
)
PY_FUNCTION_RE = re.compile(
    r"^(?P<indent>[ \t]*)(?:async\s+)?def\s+(?P<name>[A-Za-z_]\w*)"
    r"\s*\((?P<args>[^()\n]*(?:\([^()\n]*\)[^()\n]*)*)\)",
    re.MULTILINE,
)
CS_METHOD_RE = re.compile(
    r"^[ \t]*(?:(?:public|private|protected|internal|static|async|virtual|override|"
    r"sealed|new|partial|extern|unsafe|abstract|readonly)\s+)+"
    r"[\w<>,?.\[\]:]+\s+(?P<name>[A-Za-z_]\w*)\s*"
    r"\((?P<args>[^()\n]*(?:\([^()\n]*\)[^()\n]*)*)\)\s*(?:where\b[^{};]+)?(?:\{|=>|;)",
    re.MULTILINE,
)
JS_FUNCTION_RE = re.compile(
    r"^\s*(?:export\s+)?(?:default\s+)?(?:async\s+)?function\s+"
    r"(?P<name>[A-Za-z_$][\w$]*)\s*\((?P<args>[^()\n]*)\)",
    re.MULTILINE,
)
ARROW_FUNCTION_RE = re.compile(
    r"^\s*(?:export\s+)?(?:const|let|var)\s+(?P<name>[A-Za-z_$][\w$]*)\s*=\s*"
    r"(?:async\s*)?(?:\((?P<args>[^()\n]*)\)|(?P<single>[A-Za-z_$][\w$]*))\s*=>",
    re.MULTILINE,
)
METHOD_RE = re.compile(
    r"^[ \t]*(?:(?:public|private|protected|internal|static|async|virtual|override|"
    r"readonly|abstract|export|final|inline|constexpr|friend)\s+)*"
    r"(?:(?:[\w$<>,?.\[\]:*&]+\s+)+)?(?P<name>[A-Za-z_$][\w$]*)\s*"
    r"\((?P<args>[^()\n]*(?:\([^()\n]*\)[^()\n]*)*)\)\s*(?:\{|=>)",
    re.MULTILINE,
)
INVOCATION_RE = re.compile(r"\b([A-Za-z_$][\w$]*)\s*\(")
IDENTIFIER_RE = re.compile(r"\b[A-Za-z_][A-Za-z0-9_]*\b")
MARKDOWN_LINK_RE = re.compile(r"!?\[[^\]]*\]\(([^)\s]+)(?:\s+[^)]*)?\)")
SKIP_CALLS = {
    "if", "for", "foreach", "while", "switch", "catch", "using", "lock", "return",
    "nameof", "typeof", "sizeof", "new", "super", "this", "base", "assert",
}


def file_paths() -> list[Path]:
    result = subprocess.run(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
        cwd=ROOT,
        check=True,
        capture_output=True,
    )
    paths = []
    for item in result.stdout.decode("utf-8", errors="replace").split("\0"):
        if not item or item.startswith(SKIP_PREFIX):
            continue
        path = PurePosixPath(item)
        if any(part in IGNORED_PATH_PARTS for part in path.parts):
            continue
        absolute = ROOT.joinpath(*path.parts)
        if absolute.is_file():
            paths.append(absolute)
    return sorted(paths, key=lambda path: path.relative_to(ROOT).as_posix().casefold())


def component_for(path: str) -> tuple[str, str, str]:
    first = PurePosixPath(path).parts[0] if "/" in path else ""
    label, summary = COMPONENTS.get(first, ROOT_COMPONENT)
    return first, label, summary


def file_kind(path: str) -> str:
    suffix = Path(path).suffix.lower()
    if suffix in DOCUMENT_EXTENSIONS:
        return "document"
    if suffix in CODE_EXTENSIONS:
        return "code"
    if suffix in {".json", ".toml", ".yml", ".yaml", ".ini", ".xml", ".props", ".csproj"}:
        return "config"
    return "asset"


def split_arguments(value: str) -> list[str]:
    args = []
    start = 0
    depth = 0
    for index, char in enumerate(value):
        if char in "<([{":
            depth += 1
        elif char in ">)]}":
            depth = max(0, depth - 1)
        elif char == "," and depth == 0:
            args.append(value[start:index].strip())
            start = index + 1
    if value[start:].strip():
        args.append(value[start:].strip())
    return args


def parameter_details(argument: str, index: int, extension: str) -> tuple[str, str]:
    argument = re.sub(r"^\s*(?:this|params|ref|out|in|scoped|readonly)\s+", "", argument)
    argument = argument.split("=", 1)[0].strip()
    if extension in {".py", ".pyi", ".ts", ".tsx", ".astro"} and ":" in argument:
        name, parameter_type = argument.split(":", 1)
        name_match = re.search(r"[*]*([A-Za-z_$][\w$]*)", name.strip())
        if name_match:
            return name_match.group(1), parameter_type.strip()
    tokens = re.findall(r"[A-Za-z_$][\w$]*(?:[?.<>,\[\]]*)?", argument)
    if not tokens:
        return f"paramètre {index + 1}", argument
    name = tokens[-1].lstrip("@")
    type_name = argument[:argument.rfind(tokens[-1])].strip() if len(tokens) > 1 else ""
    return name, type_name


def function_end(lines: list[str], start_line: int, indent: int, extension: str) -> int:
    if extension in {".py", ".pyi"}:
        for index in range(start_line + 1, len(lines)):
            if lines[index].strip() and len(lines[index]) - len(lines[index].lstrip()) <= indent:
                return index
        return len(lines)
    opening = start_line
    for index in range(start_line, min(len(lines), start_line + 12)):
        if "{" in lines[index] or "=>" in lines[index]:
            opening = index
            break
    depth = 0
    found = False
    for index in range(opening, len(lines)):
        for char in lines[index]:
            if char == "{":
                depth += 1
                found = True
            elif char == "}":
                depth -= 1
                if found and depth <= 0:
                    return index + 1
    return min(len(lines), opening + 1)


def extract_symbols(path: str, text: str) -> list[dict]:
    suffix = Path(path).suffix.lower()
    lines = text.splitlines()
    declarations = []
    for match in CLASS_RE.finditer(text):
        line = text.count("\n", 0, match.start()) + 1
        indent = len(match.group(0)) - len(match.group(0).lstrip())
        declarations.append({
            "kind": "class",
            "name": match.group("name"),
            "line": line,
            "args": match.group("args") or "",
            "indent": indent,
            "end": function_end(lines, line - 1, indent, suffix),
        })
    if suffix in {".py", ".pyi"}:
        patterns = [PY_FUNCTION_RE]
    elif suffix in {".cs", ".cpp", ".h", ".hpp", ".c"}:
        patterns = [CS_METHOD_RE, METHOD_RE]
    elif suffix in {".ts", ".tsx", ".js", ".mjs", ".astro"}:
        patterns = [JS_FUNCTION_RE, ARROW_FUNCTION_RE, METHOD_RE]
    else:
        patterns = []
    for pattern in patterns:
        for match in pattern.finditer(text):
            name = match.group("name")
            line = text.count("\n", 0, match.start()) + 1
            if name in SKIP_CALLS or any(
                item["kind"] == "function" and item["line"] == line for item in declarations
            ):
                continue
            indent = len(match.group(0)) - len(match.group(0).lstrip())
            args = match.groupdict().get("args") or match.groupdict().get("single") or ""
            declarations.append({
                "kind": "function",
                "name": name,
                "line": line,
                "args": args,
                "indent": indent,
                "end": function_end(lines, line - 1, indent, suffix),
            })
    unique = {}
    for declaration in declarations:
        key = (declaration["kind"], declaration["line"], declaration["name"])
        unique.setdefault(key, declaration)
    classes = {
        (item["line"], item["name"])
        for item in unique.values()
        if item["kind"] == "class"
    }
    return sorted(
        (
            item for item in unique.values()
            if item["kind"] != "function" or (item["line"], item["name"]) not in classes
        ),
        key=lambda item: (item["line"], item["kind"] != "class", item["name"]),
    )


def make_node(node_id: str, label: str, kind: str, **extra: object) -> dict:
    return {"id": node_id, "label": label, "type": kind, **extra}


def normalize_identifier(value: str) -> str:
    return re.sub(r"[^a-z0-9]", "", value.casefold())


def snake_case(value: str) -> str:
    return re.sub(r"(?<!^)(?=[A-Z])", "_", value).casefold()


def declared_dependencies(text: str) -> tuple[dict[str, tuple[str, ...]], int]:
    tree = ast.parse(text)
    weights = []
    dependencies = {}
    dependency_line = 1
    for statement in tree.body:
        target = None
        value = None
        if isinstance(statement, ast.AnnAssign) and isinstance(statement.target, ast.Name):
            target, value = statement.target.id, statement.value
        elif isinstance(statement, ast.Assign) and len(statement.targets) == 1 and isinstance(statement.targets[0], ast.Name):
            target, value = statement.targets[0].id, statement.value
        if target == "WEIGHTS" and isinstance(value, ast.Dict):
            weights = [key.value for key in value.keys if isinstance(key, ast.Constant) and isinstance(key.value, str)]
        if target == "COMPOSITE_DEPENDENCIES" and isinstance(value, ast.Dict):
            for key, dependencies_ast in zip(value.keys, value.values):
                if not isinstance(key, ast.Constant) or not isinstance(key.value, str):
                    continue
                if isinstance(dependencies_ast, (ast.Tuple, ast.List)):
                    dependencies[key.value] = tuple(
                        item.value for item in dependencies_ast.elts
                        if isinstance(item, ast.Constant) and isinstance(item.value, str)
                    )
                elif (
                    isinstance(dependencies_ast, ast.Call)
                    and isinstance(dependencies_ast.func, ast.Name)
                    and dependencies_ast.func.id == "tuple"
                    and dependencies_ast.args
                    and isinstance(dependencies_ast.args[0], ast.Name)
                    and dependencies_ast.args[0].id == "WEIGHTS"
                ):
                    dependencies[key.value] = tuple(weights)
                dependency_line = statement.lineno
        if isinstance(statement, ast.Assign):
            for assigned in statement.targets:
                if (
                    isinstance(assigned, ast.Subscript)
                    and isinstance(assigned.value, ast.Name)
                    and assigned.value.id == "COMPOSITE_DEPENDENCIES"
                    and isinstance(assigned.slice, ast.Constant)
                    and assigned.slice.value == "EmergenceScore"
                ):
                    dependency_line = statement.lineno
    if "EmergenceScore" in dependencies:
        dependencies["EmergenceScore"] = tuple(sorted({
            source
            for contribution in weights
            for source in dependencies.get(contribution, ())
        }))
    return dependencies, dependency_line


def main() -> None:
    nodes: list[dict] = []
    edges: list[dict] = []
    paths = file_paths()
    path_to_id: dict[str, str] = {}
    directory_ids: dict[str, str] = {}
    component_ids: dict[str, str] = {}
    sources: dict[str, tuple[str, str, list[str]]] = {}

    project_id = "project:livex"
    nodes.append(make_node(
        project_id,
        "LIVEX",
        "project",
        summary="Monorepo de simulation déterministe, analyse, orchestration et rendu interactif.",
        path="",
    ))

    for absolute in paths:
        relative = absolute.relative_to(ROOT).as_posix()
        component_key, component_label, component_summary = component_for(relative)
        if component_key not in component_ids:
            component_id = f"component:{component_key or 'root'}"
            component_ids[component_key] = component_id
            nodes.append(make_node(
                component_id,
                component_label,
                "component",
                path=component_key,
                component=component_key or "root",
                summary=component_summary,
                parent=project_id,
            ))
            edges.append({"source": project_id, "target": component_id, "type": "contains"})

        parent_id = component_ids[component_key]
        parts = PurePosixPath(relative).parts
        accumulated = []
        for part in parts[:-1]:
            accumulated.append(part)
            directory = "/".join(accumulated)
            if directory == component_key:
                continue
            if directory not in directory_ids:
                directory_id = f"directory:{directory}"
                directory_ids[directory] = directory_id
                nodes.append(make_node(
                    directory_id,
                    part,
                    "directory",
                    path=directory,
                    component=component_key or "root",
                    summary=f"Dossier {directory}",
                    parent=parent_id,
                ))
                edges.append({"source": parent_id, "target": directory_id, "type": "contains"})
            parent_id = directory_ids[directory]

        node_id = f"file:{relative}"
        path_to_id[relative] = node_id
        suffix = absolute.suffix.lower()
        size = absolute.stat().st_size
        summary = f"{file_kind(relative)} · {size:,} octets".replace(",", " ")
        text = ""
        if suffix in TEXT_EXTENSIONS and size <= 2_000_000:
            try:
                text = absolute.read_text(encoding="utf-8")
            except (UnicodeDecodeError, OSError):
                text = ""
        if text:
            sources[relative] = (node_id, text, text.splitlines())
            if suffix in DOCUMENT_EXTENSIONS:
                title = next((line.strip("# ").strip() for line in text.splitlines() if line.startswith("#")), "")
                paragraph = next((line.strip() for line in text.splitlines() if line.strip() and not line.startswith("#")), "")
                summary = " — ".join(part for part in (title, paragraph[:180]) if part) or summary
        nodes.append(make_node(
            node_id,
            PurePosixPath(relative).name,
            "file",
            subtype=file_kind(relative),
            path=relative,
            component=component_key or "root",
            summary=summary,
            parent=parent_id,
            size=size,
        ))
        edges.append({"source": parent_id, "target": node_id, "type": "contains"})

    symbol_ids_by_name: dict[str, list[str]] = {}
    parsed: dict[str, list[dict]] = {}
    for relative, (file_id, text, _) in sources.items():
        if Path(relative).suffix.lower() not in CODE_EXTENSIONS:
            continue
        declarations = extract_symbols(relative, text)
        parsed[relative] = declarations
        component_key, _, _ = component_for(relative)
        classes = []
        for declaration in declarations:
            symbol_id = f"symbol:{relative}:{declaration['line']}:{declaration['name']}"
            declaration["id"] = symbol_id
            declaration["component"] = component_key or "root"
            declaration["file_id"] = file_id
            declaration["file"] = relative
            if declaration["kind"] == "class":
                classes.append(declaration)
                parent_id = file_id
            else:
                containing = [
                    candidate for candidate in classes
                    if candidate["line"] < declaration["line"]
                    and candidate["end"] >= declaration["line"]
                ]
                parent_id = containing[-1]["id"] if containing else file_id
            symbol_ids_by_name.setdefault(declaration["name"], []).append(symbol_id)
            nodes.append(make_node(
                symbol_id,
                declaration["name"],
                declaration["kind"],
                path=relative,
                line=declaration["line"],
                component=component_key or "root",
                summary=f"{'Type' if declaration['kind'] == 'class' else 'Déclaration'} "
                f"{declaration['name']} · {relative}:{declaration['line']}",
                parent=parent_id,
                signature=declaration["args"],
            ))
            edges.append({"source": parent_id, "target": symbol_id, "type": "contains"})
            if declaration["args"]:
                for index, argument in enumerate(split_arguments(declaration["args"])):
                    parameter, parameter_type = parameter_details(
                        argument, index, Path(relative).suffix.lower()
                    )
                    parameter_id = f"parameter:{relative}:{declaration['line']}:{index}:{parameter}"
                    nodes.append(make_node(
                        parameter_id,
                        parameter,
                        "parameter",
                        path=relative,
                        line=declaration["line"],
                        component=component_key or "root",
                        summary=f"Paramètre de {declaration['name']}"
                        + (f" · type déclaré : {parameter_type}" if parameter_type else ""),
                        parent=symbol_id,
                        signature=argument,
                    ))
                    edges.append({"source": symbol_id, "target": parameter_id, "type": "parameter"})

    for relative, (file_id, text, lines) in sources.items():
        suffix = Path(relative).suffix.lower()
        if suffix in DOCUMENT_EXTENSIONS:
            for match in MARKDOWN_LINK_RE.finditer(text):
                target = unquote(match.group(1).split("#", 1)[0])
                if not target or re.match(r"^[a-z]+:", target, re.IGNORECASE) or target.startswith("#"):
                    continue
                normalized = str(PurePosixPath(relative).parent / target)
                while "/../" in f"/{normalized}/":
                    normalized = re.sub(r"[^/]+/\.\./", "", normalized, count=1)
                normalized = normalized.removeprefix("./")
                target_id = path_to_id.get(normalized)
                if target_id and target_id != file_id:
                    line = text.count("\n", 0, match.start()) + 1
                    edges.append({
                        "source": file_id,
                        "target": target_id,
                        "type": "document",
                        "label": f"lien documentaire · ligne {line}",
                    })

        declarations = parsed.get(relative, [])
        if not declarations or suffix not in CODE_EXTENSIONS:
            continue
        for line_number, source_line in enumerate(lines, start=1):
            for invocation in INVOCATION_RE.finditer(source_line):
                name = invocation.group(1)
                if name in SKIP_CALLS or len(name) < 3:
                    continue
                candidates = symbol_ids_by_name.get(name, [])
                if len(candidates) != 1:
                    continue
                target_id = candidates[0]
                caller = next((
                    item for item in declarations
                    if item["kind"] == "function"
                    and item["line"] <= line_number < item["end"]
                ), None)
                source_id = caller["id"] if caller else file_id
                if source_id == target_id:
                    continue
                edges.append({
                    "source": source_id,
                    "target": target_id,
                    "type": "call",
                    "label": f"appel candidat · ligne {line_number}",
                })

    doc_sources = {
        path: (file_id, text, lines)
        for path, (file_id, text, lines) in sources.items()
        if Path(path).suffix.lower() in DOCUMENT_EXTENSIONS
    }
    documentation_mentions: dict[str, list[tuple[str, str, int]]] = {}
    for relative, (file_id, _, lines) in doc_sources.items():
        seen_in_document = set()
        for line_number, line in enumerate(lines, start=1):
            for match in IDENTIFIER_RE.finditer(line):
                normalized = normalize_identifier(match.group(0))
                if normalized and normalized not in seen_in_document:
                    documentation_mentions.setdefault(normalized, []).append(
                        (relative, file_id, line_number)
                    )
                    seen_in_document.add(normalized)

    code_terms: dict[str, str] = {}
    for relative, (_, _, lines) in sources.items():
        if Path(relative).suffix.lower() not in CODE_EXTENSIONS:
            continue
        for line in lines:
            for match in IDENTIFIER_RE.finditer(line):
                term = match.group(0)
                if len(term) >= 7 and any(char.isupper() for char in term[1:]):
                    code_terms.setdefault(normalize_identifier(term), term)

    shared_terms = {
        normalized: term for normalized, term in code_terms.items()
        if normalized in documentation_mentions
    }
    code_mentions: dict[str, list[tuple[str, str, int]]] = {}
    for relative, (file_id, _, lines) in sources.items():
        if Path(relative).suffix.lower() not in CODE_EXTENSIONS:
            continue
        declarations = parsed.get(relative, [])
        seen_targets = set()
        for line_number, line in enumerate(lines, start=1):
            for match in IDENTIFIER_RE.finditer(line):
                normalized = normalize_identifier(match.group(0))
                if normalized not in shared_terms:
                    continue
                owner = next((
                    item for item in declarations
                    if item["line"] <= line_number < item["end"]
                ), None)
                target_id = owner["id"] if owner else file_id
                key = (normalized, target_id)
                if key in seen_targets:
                    continue
                seen_targets.add(key)
                code_mentions.setdefault(normalized, []).append(
                    (relative, target_id, line_number)
                )

    for normalized, term in sorted(shared_terms.items()):
        code_matches = code_mentions.get(normalized, [])
        documentation_matches = documentation_mentions.get(normalized, [])
        if not code_matches:
            continue

        component_counts: dict[str, int] = {}
        for relative, _, _ in code_matches:
            component_key, _, _ = component_for(relative)
            component_counts[component_key] = component_counts.get(component_key, 0) + 1
        component_key = max(component_counts, key=component_counts.get)
        concept_id = f"concept:{normalized}"
        aliases = sorted({snake_case(term), term[:1].lower() + term[1:], term})
        nodes.append(make_node(
            concept_id,
            term,
            "concept",
            component=component_key or "root",
            summary=f"Notion présente dans {len(code_matches)} éléments de code et "
            f"{len(documentation_matches)} documents.",
            parent=component_ids.get(component_key, project_id),
            aliases=aliases,
        ))
        edges.append({
            "source": component_ids.get(component_key, project_id),
            "target": concept_id,
            "type": "contains",
        })
        for relative, target_id, line_number in code_matches:
            edges.append({
                "source": concept_id,
                "target": target_id,
                "type": "reference",
                "label": f"référencé dans le code · ligne {line_number}",
                "sourcePath": relative,
                "line": line_number,
            })
        for relative, target_id, line_number in documentation_matches:
            edges.append({
                "source": concept_id,
                "target": target_id,
                "type": "document",
                "label": f"mentionné dans la documentation · ligne {line_number}",
                "sourcePath": relative,
                "line": line_number,
            })

    concept_ids = {
        normalize_identifier(node["label"]): node["id"]
        for node in nodes
        if node["type"] == "concept"
    }
    dependency_source = sources.get("echos/echos/analysis/emergence.py")
    if dependency_source:
        dependency_data, dependency_line = declared_dependencies(dependency_source[1])
        for output, inputs in dependency_data.items():
            output_id = concept_ids.get(normalize_identifier(output))
            if not output_id:
                continue
            for dependency in inputs:
                dependency_id = concept_ids.get(normalize_identifier(dependency))
                if dependency_id:
                    edges.append({
                        "source": output_id,
                        "target": dependency_id,
                        "type": "depends",
                        "label": f"dépend de · COMPOSITE_DEPENDENCIES · ligne {dependency_line}",
                        "sourcePath": "echos/echos/analysis/emergence.py",
                        "line": dependency_line,
                    })

    for source_key, target_key, label, kind in ARCHITECTURE_FLOWS:
        source_id = component_ids.get(source_key)
        target_id = component_ids.get(target_key)
        if source_id and target_id:
            edges.append({
                "source": source_id,
                "target": target_id,
                "type": kind,
                "label": label,
                "sourcePath": "docs/ARCHITECTURE.md",
            })

    unique_edges = []
    seen_edges = set()
    for edge in edges:
        key = (edge["source"], edge["target"], edge["type"])
        if key in seen_edges:
            continue
        seen_edges.add(key)
        unique_edges.append(edge)

    data = {
        "meta": {
            "name": "LIVEX",
            "generated": datetime.now().astimezone().isoformat(timespec="seconds"),
            "files": len(paths),
            "nodes": len(nodes),
            "edges": len(unique_edges),
            "source": "git ls-files --cached --others --exclude-standard",
        },
        "nodes": nodes,
        "edges": unique_edges,
    }
    OUTPUT.write_text(
        "window.LIVEX_GRAPH_DATA = "
        + json.dumps(data, ensure_ascii=False, separators=(",", ":"))
        + ";\n",
        encoding="utf-8",
    )
    print(f"{len(paths)} fichiers · {len(nodes)} nœuds · {len(unique_edges)} relations")
    print(f"Données écrites dans {OUTPUT.relative_to(ROOT).as_posix()}")


if __name__ == "__main__":
    main()
