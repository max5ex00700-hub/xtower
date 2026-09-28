using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

static class Run
{
    static void Main(string[] files)
    {
        int count = XTapCombatChecks.Run();
        int parsed = 0;
        foreach (string file in files)
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: new[] { "UNITY_EDITOR", "UNITY_ANDROID" }));
            foreach (var d in tree.GetDiagnostics()) if (d.Severity == DiagnosticSeverity.Error) throw new Exception(file + ": " + d);
            if (Path.GetFileName(file) == "XTapBattleController.cs")
            {
                var methods = new HashSet<string>(tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Select(m => m.Identifier.Text));
                methods.UnionWith(new[] { "StartCoroutine", "StopCoroutine", "Destroy", "GetComponent", "FindObjectOfType", "Instantiate", "StopAllCoroutines", "nameof" });
                foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var direct = call.Expression as IdentifierNameSyntax;
                    if (direct != null && !methods.Contains(direct.Identifier.Text)) throw new Exception("Unresolved controller-local call: " + direct.Identifier.Text);
                }
            }
            parsed++;
        }
        int integrated = CombatProbeChecks.Run();
        Console.WriteLine("PASS: " + count + " actual combat-rule checks; " + integrated + " actual controller-method adapter checks; " + parsed + " C# files syntax parsed.");
        Console.WriteLine("Controller-local calls resolved. Unity API compilation, real rendering/audio/touch, Cloud Build and APK remain untested.");
    }
}
