param([Parameter(Mandatory)][string]$LabDirectory,[Parameter(Mandatory)][string]$BuildDirectory,[Parameter(Mandatory)][string]$CoreBuildDirectory)
$ErrorActionPreference='Stop'
$lab=(Resolve-Path -LiteralPath $LabDirectory).Path.TrimEnd('\')
$build=(Resolve-Path -LiteralPath $BuildDirectory).Path
$coreBuild=(Resolve-Path -LiteralPath $CoreBuildDirectory).Path
$config=Get-Content -LiteralPath "$lab\.settings-live-lab.json" -Raw | ConvertFrom-Json
if($config.ApplicationDirectory -ne $lab -or $config.ProfileDirectory -ne "$lab\profile" -or !(Test-Path -LiteralPath "$lab\.selection-probe-lab")){throw 'Requires prepared isolated Designer.'}
if($lab.StartsWith($env:ProgramFiles,[StringComparison]::OrdinalIgnoreCase)){throw 'Never modify Program Files'}
if((Get-FileHash "$lab\Q-Sys Designer.dll").Hash -ne '0E7150F8DC02E0159E019188780FB003CE025F5EC18C491609CF337D849BDA60' -or (Get-FileHash "$lab\ModuleCommon.dll").Hash -ne 'A6D1ADDDA06BDCB1AD32B57C7480C947210C3D3AD287F5818C2EA64068BF5AEE'){throw 'Unexpected Designer'}
$running=Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($lab+'\',[StringComparison]::OrdinalIgnoreCase) -and $_.Name -in @('Q-Sys Designer.exe','CefSharp.BrowserSubprocess.exe') }
if($running){throw 'Close the existing disposable Designer before preparing this fixture.'}
$archive=Join-Path $lab ('clipboard-previous-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $archive | Out-Null
$paths=@('profile','mods-lab-settings','live-evidence','clipboard-evidence') | ForEach-Object { Join-Path $lab $_ }
$paths+=@(Get-ChildItem -LiteralPath $lab -Filter 'Designer.Modules*.dll' -File | Select-Object -ExpandProperty FullName)
foreach($path in $paths){
 $resolved=[IO.Path]::GetFullPath($path)
 if(!$resolved.StartsWith($lab+'\',[StringComparison]::OrdinalIgnoreCase) -or !$archive.StartsWith($lab+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Archive escapes lab'}
 if(Test-Path -LiteralPath $resolved){Move-Item -LiteralPath $resolved -Destination $archive}
}
New-Item -ItemType Directory -Force -Path "$lab\profile\Temp","$lab\profile\Documents\QSC\Q-Sys Designer" | Out-Null
foreach($artifact in @('Designer.Modules.ClipboardCompat\release\Designer.Modules.ClipboardCompat.dll','Probe\release\Designer.Modules.ClipboardLab.dll','Qmm.Shared.ModsCore\release\Qmm.mods-core.Runtime.dll')){Copy-Item -LiteralPath (Join-Path $build $artifact) -Destination $lab}
foreach($artifact in @('ProfileHook\release\ProfileHook.dll','Designer.Modules.ModsCore\release\Designer.Modules.ModsCore.dll')){Copy-Item -LiteralPath (Join-Path $coreBuild $artifact) -Destination $lab}
$psi=[Diagnostics.ProcessStartInfo]::new("$lab\Q-Sys Designer.exe")
$psi.UseShellExecute=$false; $psi.WorkingDirectory=$lab; $psi.WindowStyle='Hidden'
$psi.Environment['DOTNET_STARTUP_HOOKS']="$lab\ProfileHook.dll"
$psi.Environment['DOTNET_ReadyToRun']='0'; $psi.Environment['DOTNET_TieredCompilation']='0'; $psi.Environment['COMPlus_JitNoInline']='1'
$psi.Environment['TEMP']="$lab\profile\Temp"; $psi.Environment['TMP']="$lab\profile\Temp"
$p=[Diagnostics.Process]::Start($psi)
@{ProcessId=$p.Id;ApplicationDirectory=$lab;Archive=$archive} | ConvertTo-Json | Set-Content -LiteralPath "$lab\clipboard-lab-process.json"
$p.Id
