"""Read-only filesystem/GUID audit; writes reports, never deletes Unity assets.

Run from the repository root: python Tools/ProjectAudit/audit.py
Complement with Tools > Twin Edge > Audit project dependencies in Unity.
"""
import csv
import hashlib
import json
from collections import defaultdict
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Docs/Development/Architecture/V1/Audit'
GUID = re.compile(rb'guid:\s*([0-9a-fA-F]{32})')
GENERATED = 'Assets/Game/Level/Data/Generated/'


def git(*args):
    return subprocess.check_output(['git', '-C', str(ROOT), *args])


def write_csv(name, fields, rows):
    with (OUT / name).open('w', encoding='utf-8', newline='') as stream:
        writer = csv.DictWriter(stream, fields)
        writer.writeheader()
        writer.writerows(rows)


def run():
    OUT.mkdir(parents=True, exist_ok=True)
    tracked = set(git('ls-files', '-z').decode('utf-8').split('\0'))
    files = sorted(p for folder in ('Assets', 'Packages', 'ProjectSettings', 'Docs', 'Tools')
                   for p in (ROOT / folder).rglob('*') if p.is_file() and OUT not in p.parents)
    files += [ROOT / '.gitignore', ROOT / 'README.md']
    paths = {p.relative_to(ROOT).as_posix(): p for p in files}
    guid_paths = defaultdict(list)
    for name, path in paths.items():
        if name.endswith('.meta'):
            match = GUID.search(path.read_bytes())
            if match:
                guid_paths[match[1].decode().lower()].append(name[:-5])
    edges = {}
    incoming = defaultdict(list)
    unresolved = defaultdict(set)
    for name, path in paths.items():
        refs = set(g.decode().lower() for g in GUID.findall(path.read_bytes()))
        edges[name] = set()
        for guid in refs:
            if guid in guid_paths:
                for target in guid_paths[guid]:
                    if target != name.removesuffix('.meta'):
                        edges[name].add(target)
                        incoming[target].append(name)
            elif guid not in ('0' * 32, '0' * 16 + 'e' + '0' * 15, '0' * 16 + 'f' + '0' * 15):
                unresolved[guid].add(name)
    # Retain outputs referenced transitively from ANY non-generated retained asset.
    roots = {n for n in paths if n.startswith('Assets/') and not n.startswith(GENERATED) and not n.endswith('.meta')}
    seen, pending = set(roots), list(roots)
    while pending:
        for target in edges.get(pending.pop(), ()):
            if target not in seen:
                seen.add(target)
                pending.append(target)
    generated = [n for n in paths if n.startswith(GENERATED) and not n.endswith('.meta')]
    groups = defaultdict(list)
    for name in generated:
        groups[hashlib.sha256(paths[name].read_bytes()).hexdigest()].append(name)
    duplicates = [v for v in groups.values() if len(v) > 1]
    write_csv('GeneratedAssets.csv', ['path', 'bytes', 'tracked', 'guid_reachable', 'direct_users'], [
        dict(path=n, bytes=paths[n].stat().st_size, tracked=n in tracked, guid_reachable=n in seen,
             direct_users=';'.join(sorted(incoming[n]))) for n in generated])
    runs = []
    for folder in sorted((ROOT / GENERATED).glob('Run_*')):
        if not folder.is_dir():
            continue
        names = [n for n in generated if n.startswith(folder.relative_to(ROOT).as_posix() + '/')]
        runs.append(dict(run=folder.name, assets=len(names), bytes=sum(paths[n].stat().st_size for n in names),
                         bytes_with_meta=sum(p.stat().st_size for p in folder.rglob('*') if p.is_file()),
                         guid_reachable=sum(n in seen for n in names), tracked=sum(n in tracked for n in names)))
    write_csv('GeneratedRuns.csv', ['run', 'assets', 'bytes', 'bytes_with_meta', 'guid_reachable', 'tracked'], runs)
    (OUT / 'DuplicateGeneratedAssets.json').write_text(json.dumps(duplicates, indent=2), encoding='utf-8')
    inventory = []
    for name, path in sorted(paths.items()):
        if name.startswith(GENERATED):
            category, evidence = 'Generated dependencies', 'GUID reachability in GeneratedAssets.csv; Unity report is authoritative for imports'
        elif name.startswith(('Docs/', 'Assets/_Project/Docs/', 'README')):
            category, evidence = 'Documentation', 'Design, tool documentation, or historical prototype guide'
        elif name.startswith(('Assets/Game/Level/Editor/', 'Assets/_Project/Editor/', 'Tools/')):
            category, evidence = 'Development tools', 'Editor directory or external audit script; prototype builders remain legacy tools'
        elif name.startswith('Assets/Game/Level/'):
            category, evidence = 'Active development', 'Level generator source, configs, or retained V1/V2 scenes; drafts retained as references'
        elif name.startswith('Assets/_Project/'):
            category, evidence = 'Legacy prototype', 'Present in prototype Git tree; scene script GUID references and builders confirm role'
        elif name.startswith(('Assets/Settings', 'Assets/DefaultNetworkPrefabs', 'Assets/InputSystem', 'Packages/', 'ProjectSettings/')):
            category, evidence = 'Shared infrastructure', 'Rendering, input, network registry, package or shared Unity configuration'
        elif name == '.gitignore':
            category, evidence = 'Shared infrastructure', 'Repository tracking policy'
        else:
            category, evidence = 'Unknown or requires manual review', 'No ownership asserted from name alone'
        inventory.append(dict(path=name, category=category, tracked=name in tracked, bytes=path.stat().st_size, evidence=evidence))
    write_csv('FileInventory.csv', ['path', 'category', 'tracked', 'bytes', 'evidence'], inventory)
    write_csv('UnresolvedGuids.csv', ['guid', 'users'], [dict(guid=g, users=';'.join(sorted(v))) for g, v in sorted(unresolved.items())])
    summary = dict(head=git('rev-parse', 'HEAD').decode().strip(), prototype=git('rev-parse', 'prototype^{commit}').decode().strip(),
                   files=len(inventory), tracked=sum(r['tracked'] for r in inventory),
                   generated_assets=len(generated), generated_bytes=sum(paths[n].stat().st_size for n in generated),
                   generated_guid_reachable=sum(n in seen for n in generated), runs=len(runs),
                   exact_duplicate_groups=len(duplicates), exact_duplicate_assets=sum(map(len, duplicates)),
                   duplicate_guids={g: v for g, v in guid_paths.items() if len(v) > 1},
                   missing_asset_meta=[n for n in paths if n.startswith('Assets/') and not n.endswith('.meta') and n + '.meta' not in paths],
                   missing_tracked_files=sorted(n for n in tracked if n and not (ROOT / n).exists()),
                   limitations='Static GUID scan misses binary serialization and dynamic paths; unresolved GUIDs can be package/builtin GUIDs. No cleanup authorization.')
    (OUT / 'FilesystemSummary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
    print(json.dumps(summary, indent=2))


if __name__ == '__main__':
    run()
