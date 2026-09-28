#!/usr/bin/env python3
"""Run actual combat C# rules/controller methods with explicit test adapters.
Usage: python tools/combat_checks/run.py /path/to/dotnet-sdk-root
This is not a Unity build. All compiler outputs are temporary.
"""
import json,re,shutil,subprocess,sys,tempfile
from pathlib import Path
repo=Path(__file__).resolve().parents[2]
assets=repo/'unity/XTapUnity/Assets'
dotnet=Path(sys.argv[1]).resolve()
compiler=sorted(dotnet.glob('sdk/*/Roslyn/bincore/csc.dll'))[-1]
refs=sorted(dotnet.glob('packs/Microsoft.NETCore.App.Ref/*/ref/net8.0'))[-1]
runtime=sorted(dotnet.glob('shared/Microsoft.NETCore.App/*'))[-1].name
controller=(assets/'Scripts/XTapBattleController.cs').read_text()
def member(name):
    match=re.search(r'^    (?:void|float|IEnumerator) '+name+r'\(',controller,re.M)
    if not match: raise RuntimeError('Missing method: '+name)
    start=controller.index('{',match.start());depth=1;end=start+1
    while depth:
        depth+=(controller[end]=='{')-(controller[end]=='}');end+=1
    return controller[match.start():end]
with tempfile.TemporaryDirectory(prefix='xtap-combat-') as tmp:
    build=Path(tmp);dll=build/'Checks.dll'
    probe=build/'ControllerMethods.cs'
    probe.write_text('using System; using System.Collections; using UnityEngine; public partial class CombatProbe {\n'+ '\n'.join(member(n) for n in ['CombatPointerDown','CombatPointerUp','ProcessGesture','RegisterCombatMistake','CombatHitStop','ResolveAttack','ResolveShieldBlock','ShieldBlockSequence','CombatCueRadiusPixels'])+'\n}')
    sources=[assets/'Scripts/XTapCombatState.cs',assets/'Editor/XTapCombatChecks.cs',Path(__file__).with_name('Run.cs'),Path(__file__).with_name('CombatProbe.cs'),probe]
    references=list(refs.glob('*.dll'))
    for name in ('Microsoft.CodeAnalysis.dll','Microsoft.CodeAnalysis.CSharp.dll'):
        p=compiler.parent/name;references.append(p);shutil.copy2(p,build/name)
    args=['-nologo','-noconfig','-nostdlib+','-langversion:9','-target:exe','-out:'+str(dll)]+['-r:'+str(p) for p in references]+list(map(str,sources))
    subprocess.run([str(dotnet/'dotnet'),str(compiler),*args],check=True)
    dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':runtime}}}))
    subprocess.run([str(dotnet/'dotnet'),str(dll),*map(str,sorted(assets.rglob('*.cs')))],check=True)
