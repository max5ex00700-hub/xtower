#!/usr/bin/env python3
"""Exercise actual codex/reward C# members with in-memory Unity adapters.
Usage: python tools/codex_checks/run.py /path/to/dotnet-sdk-root
This is not a Unity build; compilation outputs are temporary.
"""
import json
from pathlib import Path
import re
import subprocess
import sys
import tempfile

repo = Path(__file__).resolve().parents[2]
scripts = repo / 'unity/XTapUnity/Assets/Scripts'
dotnet = Path(sys.argv[1]).resolve()
compiler = sorted(dotnet.glob('sdk/*/Roslyn/bincore/csc.dll'))[-1]
refs = sorted(dotnet.glob('packs/Microsoft.NETCore.App.Ref/*/ref/net8.0'))[-1]
runtime = sorted(dotnet.glob('shared/Microsoft.NETCore.App/*'))[-1].name


def members(filename, names):
    source = (scripts / filename).read_text()
    result = []
    for name in names:
        match = re.search(r'^    (?:(?:public|static) )*(?:bool|int|string|void|List<string>) '
                          + name + r'\b', source, re.M)
        if not match:
            raise RuntimeError('Missing member: ' + name)
        start = source.index('{', match.start())
        depth, end = 1, start + 1
        while depth:
            depth += (source[end] == '{') - (source[end] == '}')
            end += 1
        result.append(source[match.start():end])
    return '\n'.join(result)


codex = members('XTapCodex.cs', ['MarkImageDiscovered', 'IsImageDiscovered',
    'IsCharacterComplete', 'NormalizeCharacter', 'CharacterKey', 'ImageKey',
    'ActionImageCount', 'AllImageCodes', 'DiscoveredImageCount', 'OpenCharacter'])
inventory = members('XTapInventory.cs', ['CharacterSlot', 'ExpansionBonus',
    'HasCodexCompletionReward', 'GrantCodexCompletionReward'])
with tempfile.TemporaryDirectory(prefix='xtap-codex-') as tmp:
    build = Path(tmp)
    probe = build / 'Probe.cs'
    probe.write_text('using System; using System.Collections.Generic; using UnityEngine;\n'
        'public sealed class Surface { public void SetActive(bool active) {} }\n'
        'public sealed class XTapCodex {\n'
        'const int CharacterCount=10; const string CharacterKeyPrefix="xtap_codex_char_";\n'
        'const string ImageKeyPrefix="xtap_codex_img_"; int selectedCharacter, galleryPageIndex;\n'
        'XTapInventory inventory; Surface listRoot=new Surface(), galleryRoot=new Surface();\n'
        'void RefreshGallery() {}\n'
        'public XTapCodex(XTapInventory bag) { inventory=bag; }\n'
        'public static List<string> Codes(int id) { return AllImageCodes(id); }\n'
        'public int Count(int id) { return DiscoveredImageCount(id); }\n'
        'public void OpenForTest(int id) { OpenCharacter(id); }\n' + codex + '\n}\n'
        'public sealed class XTapInventory {\n'
        'const string ExpansionKey="xtap_bag_extra_cells";\n'
        'const string CodexRewardKeyPrefix="xtap_codex_complete_reward_";\n'
        'public bool IsOpen; void Render() {}\n' + inventory + '\n}\n')
    dll = build / 'Checks.dll'
    args = ['-nologo', '-noconfig', '-nostdlib+', '-langversion:9', '-target:exe',
            '-out:' + str(dll)]
    args += ['-r:' + str(p) for p in refs.glob('*.dll')]
    args += [str(probe), str(Path(__file__).with_name('Run.cs'))]
    subprocess.run([str(dotnet / 'dotnet'), str(compiler), *args], check=True)
    dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime}}}))
    subprocess.run([str(dotnet / 'dotnet'), str(dll)], check=True)
