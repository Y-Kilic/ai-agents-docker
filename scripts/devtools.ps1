param(
    [string]$Action,
    [string]$Id
)

switch ($Action) {
    'start' {
        if (-not $env:ORCHESTRATOR_URL) {
            $env:ORCHESTRATOR_URL = 'http://localhost:5000'
        }
        dotnet run --project ../src/Orchestrator.API
    }
    'ui'    { dotnet run --project ../src/Orchestrator.UI }
    'stop'  {
        if (-not $Id) { Write-Host "Specify agent id"; break }
        Invoke-RestMethod -Method Post -Uri "http://localhost:5000/api/agent/$Id/stop"
    }
    default { Write-Host "Usage: devtools.ps1 [start|ui|stop <id>]" }
}
