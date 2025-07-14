param(
    [string]$Action
)

switch ($Action) {
    'start' { dotnet run --project ../src/Orchestrator.API }
    'ui'    { dotnet run --project ../src/Orchestrator.UI }
    default { Write-Host "Usage: devtools.ps1 [start|ui]" }
}
