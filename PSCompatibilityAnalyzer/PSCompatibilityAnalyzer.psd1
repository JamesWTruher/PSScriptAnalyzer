# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

#
# Module manifest for module 'CrossCompatibility'
#
# Generated by: Microsoft Corporation
#
# Generated on: 11/5/2018
#

@{

# Script module or binary module file associated with this manifest.
RootModule = 'PSCompatibilityAnalyzer.psm1'

# Version number of this module.
ModuleVersion = '0.1.0'

# Supported PSEditions (field not compatible with PS v3/4)
# CompatiblePSEditions = @('Core', 'Desktop')

# ID used to uniquely identify this module
GUID = '84f45c56-1fc4-4253-af1a-9ef78cbd4dbc'

# Author of this module
Author = 'Microsoft Corporation'

# Company or vendor of this module
CompanyName = 'Microsoft Corporation'

# Copyright statement for this module
Copyright = '(c) Microsoft Corporation'

# Description of the functionality provided by this module
Description = 'Collects and makes available information on PowerShell runtimes across platforms'

# Minimum version of the PowerShell engine required by this module
PowerShellVersion = '3.0'

# Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
FunctionsToExport = @(
    'New-PowerShellCompatibilityProfile'
    'Get-PlatformName'
    'ConvertTo-CompatibilityJson'
    'ConvertFrom-CompatibilityJson'
    'Get-PowerShellCompatibilityProfileData'
    'Get-PlatformData'
    'Get-PowerShellRuntimeData'
    'Get-OSData'
    'Get-WindowsSkuId'
    'Get-LinuxLsbInfo'
    'Get-DotNetData'
    'Get-PowerShellCompatibilityData'
    'Get-AvailableTypes'
    'Get-TypeAccelerators'
    'Get-CoreModuleData'
    'Get-AvailableModules'
    'Get-CommonParameters'
    'Get-AliasTable'
    'New-NativeCommandData'
    'New-CommonData'
    'New-RuntimeData'
    'New-ModuleData'
    'New-AliasData'
    'New-CmdletData'
    'New-FunctionData'
    'New-ParameterAliasData'
    'New-ParameterData'
    'New-ParameterSetData'
    'New-AvailableTypeData'
    'Get-FullTypeName'
    'Assert-CompatibilityProfileIsValid'
)

# Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
CmdletsToExport = @()

# Variables to export from this module
VariablesToExport = @()

# Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
AliasesToExport = @()

# List of all files packaged with this module
# FileList = @()

# Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
PrivateData = @{

    PSData = @{
    } # End of PSData hashtable

} # End of PrivateData hashtable

}
