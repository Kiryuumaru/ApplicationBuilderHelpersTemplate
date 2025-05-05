Set-StrictMode -Version 2.0; $ErrorActionPreference = "Stop"; $ConfirmPreference = "None"; trap { Write-Error $_ -ErrorAction Continue; exit 1 }

$Root = (Get-Item .).FullName
$SolutionName = (Get-Item "$Root").Name
$ApplicationBuilderHelpersTemplatePath = "$Root/.nuke/temp/ApplicationBuilderHelpersTemplate"

if (Test-Path $ApplicationBuilderHelpersTemplatePath){
   Remove-Item $ApplicationBuilderHelpersTemplatePath -Recurse -Force
}
git clone https://github.com/Kiryuumaru/ApplicationBuilderHelpersTemplate "$ApplicationBuilderHelpersTemplatePath"

New-Item -ItemType Directory -Force -Path "$Root/.nuke"; Copy-Item -Force -Recurse -Container "$ApplicationBuilderHelpersTemplatePath/.nuke/*" "$Root/.nuke"
New-Item -ItemType Directory -Force -Path "$Root/build"; Copy-Item -Force -Recurse -Container "$ApplicationBuilderHelpersTemplatePath/build/*" "$Root/build"
New-Item -ItemType Directory -Force -Path "$Root/src"; Copy-Item -Force -Recurse -Container "$ApplicationBuilderHelpersTemplatePath/src/*" "$Root/src"
Copy-Item -Force "$ApplicationBuilderHelpersTemplatePath/build.cmd" "$Root/build.cmd"
Copy-Item -Force "$ApplicationBuilderHelpersTemplatePath/build.ps1" "$Root/build.ps1"
Copy-Item -Force "$ApplicationBuilderHelpersTemplatePath/build.sh" "$Root/build.sh"
Copy-Item -Force "$ApplicationBuilderHelpersTemplatePath/global.json" "$Root/global.json"
Copy-Item -Force "$ApplicationBuilderHelpersTemplatePath/ApplicationBuilderHelpersTemplate.sln" "$Root/$SolutionName.sln"
