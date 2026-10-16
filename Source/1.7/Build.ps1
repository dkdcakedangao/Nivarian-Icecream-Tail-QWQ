param(
    [string]$RimWorldDir = 'E:\steam\steamapps\common\RimWorld',
    [string]$NivarianDir = 'E:\steam\steamapps\common\RimWorld\Mods\Nivarian',
    [string]$HarmonyAssemblyPath = 'E:\steam\steamapps\workshop\content\294100\1127530465\1.7\Assemblies\0Harmony.dll',
    [string]$HarAssemblyPath = 'E:\steam\steamapps\workshop\content\294100\839005762\1.6\Assemblies\AlienRace.dll'
)
# PowerShell 7.6/.NET 10 hosts Roslyn; this build does not require an installed SDK.
$ErrorActionPreference = 'Stop'
if ([Environment]::Version.Major -lt 10) { throw 'Run with PowerShell on .NET 10, or build the net10.0 csproj with the .NET 10 SDK.' }
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$files = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs' | Where-Object {
    $_.FullName -notmatch '\\(Tests|obj|bin|MeleeAnimationCompatibility)\\' -and $_.Name -ne 'MintBreathPreview.cs'
} | ForEach-Object { $_.FullName })
$refs = @('Assembly-CSharp.dll', 'UnityStubs.dll', 'Waylith.dll', 'Waylith.UnityCompat.dll' | ForEach-Object { Join-Path $RimWorldDir $_ })
$refs += @('1.7/Core/Assemblies/Nivarian_Race.dll', '1.7/RU/Assemblies/RimUnity.Core.dll', '1.7/RU/Assemblies/RimUnity.dll', '1.7/LumiParticle/Assemblies/LumiParticle.dll' | ForEach-Object { Join-Path $NivarianDir $_ })
$refs += @($HarmonyAssemblyPath, $HarAssemblyPath)
$refs += @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object { $_.FullName })
$target = Join-Path $sourceRoot '../1.7/Assemblies/NivarianIcecreamTail.dll'
$staging = Join-Path $sourceRoot 'obj/1.7/NivarianIcecreamTail.dll'
[void](New-Item -ItemType Directory -Force -Path (Split-Path $staging), (Split-Path $target))
if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging }
Add-Type -Path $files -ReferencedAssemblies $refs -CompilerOptions '/define:RIMWORLD_17', '/optimize+', '/debug-', '/deterministic+', '/nowarn:1701' -OutputAssembly $staging
Copy-Item -LiteralPath $staging -Destination $target -Force
foreach ($folder in @('Defs', 'Patches')) {
    Copy-Item -LiteralPath (Join-Path $sourceRoot "../1.6/$folder") -Destination (Join-Path $sourceRoot '../1.7') -Recurse -Force
}
"PASS: $($files.Count) production sources, 1.7 Release: $target"
