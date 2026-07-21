@{
    RootModule        = 'TiaAgent.Supervisor.psm1'
    ModuleVersion     = '0.1.0'
    GUID              = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'
    Author            = 'TIA Portal Code Agent'
    CompanyName       = 'TIA Portal Code Agent'
    Copyright         = '(c) 2026 TIA Portal Code Agent. All rights reserved.'
    Description       = 'Local runtime supervisor for TIA Portal Code Agent services.'
    PowerShellVersion = '5.1'
    FunctionsToExport = @(
        'Initialize-TiaAgentPaths',
        'Test-TiaAgentPrerequisites',
        'Get-TiaAgentPort',
        'New-TiaAgentRuntimeManifest',
        'Start-TiaAgentService',
        'Wait-TiaAgentHealth',
        'Read-TiaAgentSettings',
        'New-TiaAgentOpenCodeConfig',
        'Test-TiaAgentStaleRuntime',
        'Lock-TiaAgentSupervisor',
        'Write-TiaAgentLog',
        'Stop-TiaAgentService',
        'Test-TcpConnectivity'
    )
    CmdletsToExport   = @()
    VariablesToExport  = @()
    AliasesToExport    = @()
    PrivateData = @{
        PSData = @{
            Tags       = @('TIA', 'Agent', 'Supervisor', 'Runtime')
            ProjectUri = 'https://github.com/user/tia-portal-code-agent'
        }
    }
}
