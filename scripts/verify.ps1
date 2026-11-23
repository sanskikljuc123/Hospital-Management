# Runs scripts/verify.sh (the CI gates G1-G6 on this machine) through Git Bash, so there is one implementation.
# Usage: scripts\verify.ps1 [--no-frontend]
$bash = Join-Path (Split-Path (Split-Path (Get-Command git).Source)) 'bin\bash.exe'
if (-not (Test-Path $bash)) { throw "Git Bash not found next to git ($bash). Install Git for Windows." }
& $bash (Join-Path $PSScriptRoot 'verify.sh') @args
exit $LASTEXITCODE
