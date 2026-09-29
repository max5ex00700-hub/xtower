#!/usr/bin/env python3
"""Compile actual inventory/battle stat members with small Unity state adapters.
Usage: python tools/equipment_checks/run.py /path/to/dotnet-sdk-root
No Unity import, rendering, serialization or APK validation is implied.
"""
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

repo = Path(__file__).resolve().parents[2]
scripts = repo / 'unity/XTapUnity/Assets/Scripts'
dotnet = Path(sys.argv[1]).resolve()
compiler = sorted(dotnet.glob('sdk/*/Roslyn/bincore/csc.dll'))[-1]
refs = sorted(dotnet.glob('packs/Microsoft.NETCore.App.Ref/*/ref/net8.0'))[-1]
runtime = sorted(dotnet.glob('shared/Microsoft.NETCore.App/*'))[-1].name
inventory = (scripts / 'XTapInventory.cs').read_text()
controller = (scripts / 'XTapBattleController.cs').read_text()


def member(source, name):
    match = re.search(r'^    (?:(?:public|static) )*(?:bool|int|double|void) ' + name + r'\b', source, re.M)
    if not match:
        raise RuntimeError('Missing member: ' + name)
    start = source.index('{', match.start())
    depth, end = 1, start + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end]


with tempfile.TemporaryDirectory(prefix='xtap-equipment-') as tmp:
    build = Path(tmp)
    model = inventory[inventory.index('[Serializable]'):inventory.index('public static class XTapGearNameColor')]
    names = (
        'CharacterSlot', 'IsCompatibleWithBagOwner', 'CountsTowardPlayerStats',
        'DescriptorEquipScore', 'DescriptorSetMultiplier', 'ExclusiveBlockBonusPercent',
        'ExclusiveEquipmentBonusPercent', 'ExclusiveEquipmentMultiplier', 'ApplyExclusiveEquipmentBonus',
        'EquippedAttack', 'EquippedDefense', 'EquippedHp', 'GetEquippedAttack',
        'GetEquippedDefense', 'GetEquippedHp', 'GetBagDisplayStats',
        'EnsureEnhancementBaseStats', 'GetPreDescriptorStats', 'RecalculateEnhancedStats',
        'MergeSynthesisStats',
    )
    probe = build / 'ActualMembers.cs'
    probe.write_text('using System; using System.Collections.Generic; using UnityEngine;\n' + model +
        'public sealed class XTapInventory { public readonly List<XTapGearBlockData> items = new List<XTapGearBlockData>();\n' +
        '\n'.join(member(inventory, name) for name in names) + '\n}\n' +
        'public sealed partial class BattleProbe {\n' +
        '\n'.join(re.findall(r'^    const double BasePlayer\w+ = .*;', controller, re.M)) + '\n' +
        '\n'.join(member(controller, name) for name in (
            'CurrentPlayerAttack', 'CurrentPlayerDefense', 'CurrentPlayerMaxHp', 'SafeMultiply')) + '\n}\n')
    dll = build / 'Checks.dll'
    args = ['-nologo', '-noconfig', '-nostdlib+', '-langversion:9', '-target:exe', '-out:' + str(dll)]
    args += ['-r:' + str(path) for path in refs.glob('*.dll')]
    args += [str(probe), str(scripts / 'XTapStatFormat.cs'), str(Path(__file__).with_name('Run.cs'))]
    subprocess.run([str(dotnet / 'dotnet'), str(compiler), *args], check=True)
    dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime}}}))
    subprocess.run([str(dotnet / 'dotnet'), str(dll)], check=True)
