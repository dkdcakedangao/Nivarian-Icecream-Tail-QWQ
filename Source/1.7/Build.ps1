param(
    [string]$RimWorldDir = 'E:\steam\steamapps\common\RimWorld',
    [string]$NivarianDir = 'E:\steam\steamapps\common\RimWorld\Mods\Nivarian',
    [string]$HarmonyAssemblyPath = 'E:\steam\steamapps\workshop\content\294100\1127530465\1.7\Assemblies\0Harmony.dll',
    [string]$HarAssemblyPath = 'E:\steam\steamapps\workshop\content\294100\839005762\1.7\Assemblies\AlienRace.dll',
    [string]$RimUnityReferenceDir = (Join-Path $PSScriptRoot '../obj/1.7/RimUnityReferences'),
    [switch]$StageOnly
)
# PowerShell 7.6/.NET 10 hosts Roslyn; this build does not require an installed SDK.
$ErrorActionPreference = 'Stop'
if ([Environment]::Version.Major -lt 10) { throw 'Run with PowerShell on .NET 10, or build the net10.0 csproj with the .NET 10 SDK.' }
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$modRoot = (Resolve-Path (Join-Path $sourceRoot '..')).Path
$adapterNames = @('PawnAnimationData.cs','NativeAnimationRig.cs','PawnAnimationSession.cs','PawnAnimationRendering.cs','PawnAnimationGraphics.cs','MeleeAnimationInterlock.cs','PawnAnimationBackend.cs')
$files = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs' | Where-Object {
    $_.FullName -notmatch '\\(Tests|obj|bin|MeleeAnimationCompatibility)\\' -and $_.Name -ne 'MintBreathPreview.cs'
})
$coreFiles = @($files | Where-Object Name -notin $adapterNames | ForEach-Object FullName)
$adapterFiles = @($files | Where-Object Name -in $adapterNames | ForEach-Object FullName)
$refs = @('Assembly-CSharp.dll', 'UnityStubs.dll', 'Waylith.dll', 'Waylith.UnityCompat.dll' | ForEach-Object { Join-Path $RimWorldDir $_ })
$refs += @($HarmonyAssemblyPath, $HarAssemblyPath)
$refs += @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object { $_.FullName })
$stage = Join-Path $sourceRoot 'obj/1.7/SingleRuntime'
[void](New-Item -ItemType Directory -Force -Path $stage)
$main = Join-Path $stage 'NivarianIcecreamTail.dll'
$adapter = Join-Path $stage 'NivarianIcecreamTail.Animation.dll'
if (-not ('IcecreamReleaseCompiler' -as [type])) {
    $compilerRefs = @([Microsoft.CodeAnalysis.Compilation].Assembly.Location,[Microsoft.CodeAnalysis.CSharp.CSharpCompilation].Assembly.Location)
    $compilerRefs += @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object FullName)
    Add-Type -ReferencedAssemblies $compilerRefs -CompilerOptions '/nowarn:1701' -TypeDefinition @'
using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
public static class IcecreamReleaseCompiler {
    public static void Build(string name, string[] files, string[] references, string output) {
        var parse = new CSharpParseOptions(preprocessorSymbols: new[]{"RIMWORLD_17"});
        var trees = files.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), parse, file, Encoding.UTF8));
        var refs = references.Select(file => MetadataReference.CreateFromFile(file));
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, deterministic: true);
        var compilation = CSharpCompilation.Create(name, trees, refs, options);
        using (var stream = File.Create(output)) {
            var result = compilation.Emit(stream);
            if (!result.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        }
    }
}
'@
}
[IcecreamReleaseCompiler]::Build('NivarianIcecreamTail', $coreFiles, $refs, $main)
$adapterRefs = $refs + @($main, (Join-Path $NivarianDir '1.7/LumiParticle/Assemblies/LumiParticle.rimunity'))
$adapterRefs += @('RimUnity.Core.dll','RimUnity.dll' | ForEach-Object { Join-Path $RimUnityReferenceDir $_ })
[IcecreamReleaseCompiler]::Build('NivarianIcecreamTail.Animation', $adapterFiles, $adapterRefs, $adapter)
if (-not $StageOnly) {
    $release = Join-Path $modRoot '1.7/Assemblies'
    $adapterRelease = Join-Path $modRoot '1.7/Animation'
    [void](New-Item -ItemType Directory -Force -Path $release, $adapterRelease)
    # Retire only our known artifacts. Never copy another runtime provider.
    $retired = Join-Path $sourceRoot ('obj/1.7/RetiredRuntime-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    foreach ($name in @('RimUnity.Embedded.dll','NivarianIcecreamTail.rimunity','NivarianIcecreamTail.rimunity.json')) {
        $old = Join-Path $release $name
        if (Test-Path -LiteralPath $old) {
            [void](New-Item -ItemType Directory -Force -Path $retired)
            Move-Item -LiteralPath $old -Destination (Join-Path $retired $name)
        }
    }
    Copy-Item -LiteralPath $main -Destination $release -Force
    Copy-Item -LiteralPath $adapter -Destination $adapterRelease -Force
    foreach ($folder in @('Defs','Patches')) {
        Copy-Item -LiteralPath (Join-Path $modRoot "1.6/$folder") -Destination (Join-Path $modRoot '1.7') -Recurse -Force
    }
}
"PASS: core $($coreFiles.Count) sources + adapter $($adapterFiles.Count) sources; stable names; no runtime payload. Stage: $stage"
