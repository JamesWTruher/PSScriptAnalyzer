param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$BootstrapBuildEnv,
    [switch]$Publish,
    [string]$PublishDir = "${PSScriptRoot}/out",
    [switch]$Clean,
    [switch]$Test
)

$dotnetCLIChannel = "Release"
$dotnetCLIRequiredVersion = [version]$(Get-Content $PSScriptRoot/global.json | ConvertFrom-Json).Sdk.Version

function precheck([string]$command, [string]$missedMessage) {
    $c = Get-Command $command -ErrorAction SilentlyContinue
    if (-not $c) {
        if (-not [string]::IsNullOrEmpty($missedMessage))
        {
            Write-Warning $missedMessage
        }
        return $false
    } else {
        return $true
    }
}

function Install-Dotnet {
    [CmdletBinding()]
    param(
        [string]$Channel = $dotnetCLIChannel,
        [string]$Version = $dotnetCLIRequiredVersion,
        [switch]$NoSudo
    )

    # This allows sudo install to be optional; needed when running in containers / as root
    # Note that when it is null, Invoke-Expression (but not &) must be used to interpolate properly
    $sudo = if (!$NoSudo) { "sudo" }

    $obtainUrl = "https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain"

    # Install for Linux and OS X
    if ($Environment.IsLinux -or $Environment.IsMacOS) {
        # Uninstall all previous dotnet packages
        $uninstallScript = if ($Environment.IsUbuntu) {
            "dotnet-uninstall-debian-packages.sh"
        } elseif ($Environment.IsMacOS) {
            "dotnet-uninstall-pkgs.sh"
        }

        if ($uninstallScript) {
            Start-NativeExecution {
                curl -sO $obtainUrl/uninstall/$uninstallScript
                Invoke-Expression "$sudo bash ./$uninstallScript"
            }
        } else {
            Write-Warning "This script only removes prior versions of dotnet for Ubuntu 14.04 and OS X"
        }

        # Install new dotnet 1.1.0 preview packages
        $installScript = "dotnet-install.sh"
        Start-NativeExecution {
            curl -sO $obtainUrl/$installScript
            bash ./$installScript -c $Channel -v $Version
        }
    } elseif ($Environment.IsWindows) {
        Remove-Item -ErrorAction SilentlyContinue -Recurse -Force ~\AppData\Local\Microsoft\dotnet
        $installScript = "dotnet-install.ps1"
        Invoke-WebRequest -Uri $obtainUrl/$installScript -OutFile $installScript

        if (-not $Environment.IsCoreCLR) {
            & ./$installScript -Channel $Channel -Version $Version
        } else {
            # dotnet-install.ps1 uses APIs that are not supported in .NET Core, so we run it with Windows PowerShell
            $fullPSPath = Join-Path -Path $env:windir -ChildPath "System32\WindowsPowerShell\v1.0\powershell.exe"
            $fullDotnetInstallPath = Join-Path -Path $pwd.Path -ChildPath $installScript
            Start-NativeExecution { & $fullPSPath -NoLogo -NoProfile -File $fullDotnetInstallPath -Channel $Channel -Version $Version }
        }
    }
}

function Find-Dotnet() {
    $originalPath = $env:PATH
    $dotnetPath = if ($Environment.IsWindows) { "$env:LocalAppData\Microsoft\dotnet" } else { "$env:HOME/.dotnet" }

    # If there dotnet is already in the PATH, check to see if that version of dotnet can find the required SDK
    # This is "typically" the globally installed dotnet
    if (precheck dotnet) {
        # Must run from within repo to ensure global.json can specify the required SDK version
        Push-Location $PSScriptRoot
        $dotnetCLIInstalledVersion = (dotnet --version)
        Pop-Location
        if ($dotnetCLIInstalledVersion -ne $dotnetCLIRequiredVersion) {
            Write-Warning "The 'dotnet' in the current path can't find SDK version ${dotnetCLIRequiredVersion}, prepending $dotnetPath to PATH."
            # Globally installed dotnet doesn't have the required SDK version, prepend the user local dotnet location
            $env:PATH = $dotnetPath + [IO.Path]::PathSeparator + $env:PATH
        }
    }
    else {
        Write-Warning "Could not find 'dotnet', appending $dotnetPath to PATH."
        $env:PATH += [IO.Path]::PathSeparator + $dotnetPath
    }

    if (-not (precheck 'dotnet' "Still could not find 'dotnet', restoring PATH.")) {
        $env:PATH = $originalPath
    }
}


function Publish-Module {
    param ( [string]$target = "${PSScriptRoot}/out" )
    $ruleDll = "Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules.dll"
    $engineDll = "Microsoft.Windows.PowerShell.ScriptAnalyzer.dll"
    $jsonDll = "Newtonsoft.Json.dll"
    $coreBuildDir = "${PSScriptRoot}/Rules/bin/Full/netstandard1.6"
    $PSv3BuildDir = "${PSScriptRoot}/Rules/bin/PSV3Release/net451"
    $PSv5BuildDir = "${PSScriptRoot}/Rules/bin/Full/net451"

    $moduleRoot = "${target}/PSScriptAnalyzer"

    $ProductManifest = @{ Source = "${PSScriptRoot}/Engine"; Target = "$moduleRoot"; SourceFile = "PSScriptAnalyzer.psd1" },
        @{ Source = "${PSScriptRoot}/Engine"; Target = "$moduleRoot"; SourceFile = "PSScriptAnalyzer.psm1" },
        @{ Source = "${PSScriptRoot}/Engine"; Target = "$moduleRoot"; SourceFile = "ScriptAnalyzer.types.ps1xml" },
        @{ Source = "${PSScriptRoot}/Engine"; Target = "$moduleRoot"; SourceFile = "ScriptAnalyzer.format.ps1xml" },

        @{ Source = "${coreBuildDir}"; Target = "${moduleRoot}/coreclr"; SourceFile = $ruleDll },
        @{ Source = "${coreBuildDir}"; Target = "${moduleRoot}/coreclr"; SourceFile = $engineDll },

        @{ Source = "${Psv3BuildDir}"; Target = "${moduleRoot}/PSv3"; SourceFile = $ruleDll },
        @{ Source = "${Psv3BuildDir}"; Target = "${moduleRoot}/PSv3"; SourceFile = $engineDll },
        @{ Source = "${Psv3BuildDir}"; Target = "${moduleRoot}/PSv3"; SourceFile = $jsonDll },

        @{ Source = "${Psv5BuildDir}"; Target = "${moduleRoot}/PSv5"; SourceFile = $ruleDll },
        @{ Source = "${Psv5BuildDir}"; Target = "${moduleRoot}/PSv5"; SourceFile = $engineDll },
        @{ Source = "${Psv5BuildDir}"; Target = "${moduleRoot}/PSv5"; SourceFile = $jsonDll },

        @{ Source = "${PSScriptRoot}/docs"; Target = "${moduleRoot}/en-US"; SourceFile = "about_PSScriptAnalyzer.help.txt" },
        # TODO: Help.xml location

        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "CmdletDesign.psd1" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "CodeFormatting.psd1" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "CodeFormattingAllman.psd1" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "CodeFormattingOTBS.psd1" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "CodeFormattingStroustrup.psd1" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "core-6.0.0-alpha-linux.json" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "core-6.0.0-alpha-osx.json" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "core-6.0.0-alpha-windows.json" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "desktop-5.1.14393.206-windows.json" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "DSC.psd1" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "PSGallery.psd1" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "ScriptFunctions.psd1" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "ScriptingStyle.psd1" },
        @{ Source = "${PSScriptRoot}/Engine/Settings"; Target = "${moduleRoot}/Settings"; SourceFile = "ScriptSecurity.psd1" }


    foreach ( $file in $ProductManifest ) {
        $sourcePath = Join-Path $file.Source $file.SourceFile
        Write-Progress "Checking file $sourcePath"
        if ( ! (Test-Path $sourcePath) ) {
            Write-Warning "$sourcePath not found"
        }
    }

    $destinationDirs = $moduleRoot, "${moduleRoot}/en-US", "${moduleRoot}/Settings",
            "${moduleRoot}/coreclr", "${moduleRoot}/PSv3", "${moduleRoot}/PSv5"
    foreach ( $d in $destinationDirs ) { $null = new-item -type directory $d }
    foreach ( $file in $ProductManifest ) {
        $sourcePath = join-path $file.Source $file.sourcefile
        $targetPath = join-path $file.Target $file.sourcefile
        Copy-Item $sourcePath $targetPath
    }

}

###
# MAIN
###

if ( $Clean ) {
    foreach ( $directory in "Rules" ) {
        try {
            Push-Location "${PSScriptRoot}/${directory}"
            $result = dotnet clean "./${directory}.csproj"
            foreach ( $dir in "bin","obj" ) {
                if ( Test-Path "$dir" ) { Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue -Verbose }
            }
        }
        finally {
            Pop-Location
        }
    }
    exit
}

if ( $BootstrapBuildEnv ) 
{
    # be sure platyPS is available
    # be sure that Pester is installed
    if ( !(Get-Module -ListAvailable platyPS)) {
        Install-Module -Name platyPS -scope CurrentUser
    }
    $pesterModule = Get-Module -ListAvailable Pester
    if ( ! $pesterModule -or $pesterModule.Version -lt "4.1" ) {
        Install-Module -Name Pester -MinimumVersion 4.1
    }

    Find-DotNet

    Install-Dotnet
}

# build all 3 variants (PSV3, PSV5, and CORE) of Engine and Rules
# we will always build core
[array]$builds = @{ Configuration = "Full"; Framework = "netstandard1.6"; Msg = "Version 6 on netstandard1.6" }
# add net451 if we can, we can't really build this on core without the net451 target being present
if ( $psversiontable.PSEdition -ne "Core" ) {
    $builds += @{ Configuration = "PSV3Release"; Framework = "net451"; Msg = "Version 3 on net451" }
    $builds += @{ Configuration = "Full"; Framework = "net451"; Msg = "Version 5 on net451" }
}

try {
    # rules has the engine as a dependency, so we don't need to build it at all, just build the rules
    # and we get the engine
    Push-Location Rules

    foreach ( $build in $builds ) {
        Write-Progress ("Building " + $build.Msg)
        # $result = dotnet build ./Rules.csproj --framework $build.framework --configuration $build.configuration
        if ( ! $? ) {
            wait-debugger
            Write-Error "$result"
            throw "Failed Build for Core"
        }
    }
}
finally {
    Pop-Location
}

if ( $publish ) {
    Publish-Module $PublishDir
}


<#
# we build all flavors of script analyzer each time
# PSv3, PSv5, and Core

# the layout of the module is:
# ScriptRoot contains psd1, psm1, format and type files
# coreclr contains assemblies for PowerShell Core
# PSv3 contains assemblies for PSSA for PowerShell 3
# PSv5 contains assemblies for PSSA for PowerShell 5
# en-US contains english help files
# Settings contains setting files (*.psd1) for various needs
# ScriptRules contains rules created via script
# CompatibilityLibraries (term is not final)  contains library files for compatibility rules


$solutionDir = Split-Path $MyInvocation.InvocationName
if (-not (Test-Path "$solutionDir/global.json"))
{
    throw "Not in solution root"
}

$itemsToCopyBinaries = @("$solutionDir\Engine\bin\$Configuration\$Framework\Microsoft.Windows.PowerShell.ScriptAnalyzer.dll",
    "$solutionDir\Rules\bin\$Configuration\$Framework\Microsoft.Windows.PowerShell.ScriptAnalyzer.BuiltinRules.dll")

$itemsToCopyCommon = @("$solutionDir\Engine\PSScriptAnalyzer.psd1",
    "$solutionDir\Engine\PSScriptAnalyzer.psm1",
    "$solutionDir\Engine\ScriptAnalyzer.format.ps1xml",
    "$solutionDir\Engine\ScriptAnalyzer.types.ps1xml")

$destinationDir = "$solutionDir\out\PSScriptAnalyzer"
$destinationDirBinaries = $destinationDir
if ($Framework -eq "netstandard1.6")
{
    $destinationDirBinaries = "$destinationDir\coreclr"
}
elseif ($Configuration -match 'PSv3') {
    $destinationDirBinaries = "$destinationDir\PSv3"
}

if ($Restore.IsPresent)
{
    Invoke-RestoreSolution
}

if ($build)
{

    if (-not (Test-DotNetRestore((Join-Path $solutionDir Engine))))
    {
        Invoke-RestoreSolution
    }
    Push-Location Engine\
    dotnet build Engine.csproj --framework $Framework --configuration $Configuration
    Pop-Location


    if (-not (Test-DotNetRestore((Join-Path $solutionDir Rules))))
    {
        Invoke-RestoreSolution
    }
    Push-Location Rules\
    dotnet build Rules.csproj --framework $Framework --configuration $Configuration
    Pop-Location

    Function CopyToDestinationDir($itemsToCopy, $destination)
    {
        if (-not (Test-Path $destination))
        {
            New-Item -ItemType Directory $destination -Force
        }
        foreach ($file in $itemsToCopy)
        {
            Copy-Item -Path $file -Destination (Join-Path $destination (Split-Path $file -Leaf)) -Verbose -Force
        }
    }
    CopyToDestinationDir $itemsToCopyCommon $destinationDir
    CopyToDestinationDir $itemsToCopyBinaries $destinationDirBinaries

    # Copy Settings File
    Copy-Item -Path "$solutionDir\Engine\Settings" -Destination $destinationDir -Force -Recurse -Verbose

    # copy newtonsoft dll if net451 framework
    if ($Framework -eq "net451")
    {
        copy-item -path "$solutionDir\Rules\bin\$Configuration\$Framework\Newtonsoft.Json.dll" -Destination $destinationDirBinaries -Verbose
    }
}
#>
