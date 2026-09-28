#!/usr/bin/env python3
"""Compile real affinity code with a supplied .NET 8 SDK; no Unity claim.
Usage: python tools/affinity_checks/run.py /path/to/dotnet-sdk-root
All build outputs go to a temporary folder, not the Unity project.
"""
import json
import re
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

repo = Path(__file__).resolve().parents[2]
dotnet = Path(sys.argv[1]).resolve()
compiler = sorted(dotnet.glob('sdk/*/Roslyn/bincore/csc.dll'))[-1]
refs = sorted(dotnet.glob('packs/Microsoft.NETCore.App.Ref/*/ref/net8.0'))[-1]
runtime = sorted(dotnet.glob('shared/Microsoft.NETCore.App/*'))[-1].name
assets = repo / 'unity/XTapUnity/Assets'
sources = [assets/'Scripts'/f'{name}.cs' for name in ('XTapAffinityState', 'XTapJailAffinity', 'XTapJailDialogue')]
sources += [assets/'Editor/XTapAffinityChecks.cs', Path(__file__).with_name('Run.cs')]
with tempfile.TemporaryDirectory(prefix='xtap-affinity-') as tmp:
    build = Path(tmp)
    # Compile the actual inventory capacity/unlock member bodies, unchanged.
    # Rendering and collision against other blocks still require Unity testing.
    inventory = (assets/'Scripts/XTapInventory.cs').read_text()
    def member(name):
        match = re.search(r'^    (?:public |static )?(?:bool|int) '+name+r'\b', inventory, re.M)
        if not match: raise RuntimeError('Missing inventory member: '+name)
        start = inventory.index('{', match.start()); depth = 1; end = start + 1
        while depth:
            depth += (inventory[end] == '{') - (inventory[end] == '}'); end += 1
        return inventory[match.start():end]
    probe = build/'InventoryCapacityProbe.cs'
    probe.write_text('using UnityEngine; public sealed class InventoryCapacityProbe {\n'
        'const int GridW=8; const int BaseGridCells=24; const string ExpansionKey="xtap_bag_extra_cells";\n'
        'int activeBagOwnerCharacterId; public InventoryCapacityProbe(int owner) { activeBagOwnerCharacterId=owner; }\n'
        'public bool Accepts(int x,int y) { return IsCellUnlocked(x,y); }\n'
        + '\n'.join(member(name) for name in ('CharacterSlot','ExpansionBonus','GridCapacity','ActiveGridCapacity','GetBagCapacity','IsCellUnlocked')) + '\n}')
    dll = build/'Checks.dll'
    references = list(refs.glob('*.dll'))
    for name in ('Microsoft.CodeAnalysis.dll', 'Microsoft.CodeAnalysis.CSharp.dll'):
        path = compiler.parent/name
        references.append(path)
        shutil.copy2(path, build/name)
    args = ['-nologo', '-noconfig', '-nostdlib+', '-langversion:9', '-target:exe', '-out:'+str(dll)]
    args += ['-r:'+str(p) for p in references] + [str(p) for p in sources] + [str(probe)]
    subprocess.run([str(dotnet/'dotnet'), str(compiler), *args], check=True)
    dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm':'net8.0','framework': {'name':'Microsoft.NETCore.App','version':runtime}}}))
    subprocess.run([str(dotnet/'dotnet'), str(dll), *map(str, sorted(assets.rglob('*.cs')))], check=True)
