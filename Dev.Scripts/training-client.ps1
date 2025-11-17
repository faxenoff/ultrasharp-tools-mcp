#!/usr/bin/env pwsh
# Training Client for Static PGO
# Sends multiple MCP commands to Droid to exercise hot paths

param(
    [Parameter(Mandatory=$true)]
    [string]$ServerExecutable,

    [Parameter(Mandatory=$true)]
    [string]$SolutionPath
)

$ErrorActionPreference = "Stop"

function Send-McpRequest {
    param(
        [Parameter(Mandatory=$true)]
        $Process,

        [Parameter(Mandatory=$true)]
        [string]$Method,

        [Parameter(Mandatory=$false)]
        [hashtable]$Params = @{}
    )

    $id = [System.Guid]::NewGuid().ToString()

    $request = @{
        jsonrpc = "2.0"
        id = $id
        method = $method
        params = $Params
    } | ConvertTo-Json -Depth 10 -Compress

    try {
        $Process.StandardInput.WriteLine($request)
        $Process.StandardInput.Flush()
        Start-Sleep -Milliseconds 100
    }
    catch {
        Write-Warning "Failed to send request: $_"
    }
}

Write-Host "Starting training client..." -ForegroundColor Cyan
Write-Host "Server: $ServerExecutable" -ForegroundColor Gray
Write-Host "Solution: $SolutionPath" -ForegroundColor Gray

# Start server process
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $ServerExecutable
$psi.Arguments = "--load-solution `"$SolutionPath`" --log-level Warning"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi

try {
    $process.Start() | Out-Null
    Write-Host "✓ Server started (PID: $($process.Id))" -ForegroundColor Green

    # Wait for server to initialize
    Write-Host "Waiting for server initialization..." -ForegroundColor Gray
    Start-Sleep -Seconds 5

    # Send initialize request
    Write-Host "Sending initialize..." -ForegroundColor Cyan
    Send-McpRequest -Process $process -Method "initialize" -Params @{
        protocolVersion = "2024-11-05"
        capabilities = @{}
        clientInfo = @{
            name = "training-client"
            version = "1.0"
        }
    }
    Start-Sleep -Seconds 2

    # Send initialized notification
    Write-Host "Sending initialized..." -ForegroundColor Cyan
    $initialized = @{
        jsonrpc = "2.0"
        method = "notifications/initialized"
    } | ConvertTo-Json -Compress
    $process.StandardInput.WriteLine($initialized)
    $process.StandardInput.Flush()
    Start-Sleep -Seconds 2

    # List tools
    Write-Host "Requesting tools/list..." -ForegroundColor Cyan
    Send-McpRequest -Process $process -Method "tools/list"
    Start-Sleep -Seconds 1

    # Call UltrasharpTool_LoadProject for multiple projects (exercises hot paths)
    $projects = @("UltrasharpTools.Tools", "UltrasharpTools.Droid", "UltrasharpTools.Overlord")
    foreach ($projectName in $projects) {
        Write-Host "Calling UltrasharpTool_LoadProject for $projectName..." -ForegroundColor Cyan
        Send-McpRequest -Process $process -Method "tools/call" -Params @{
            name = "UltrasharpTool_LoadProject"
            arguments = @{
                projectName = $projectName
                detailLevel = "Full"
            }
        }
        Start-Sleep -Seconds 1
    }

    # Call UltrasharpTool_ViewDefinition for several types
    $types = @(
        "UltrasharpTools.Tools.Services.SolutionManager",
        "UltrasharpTools.Tools.Services.FastSymbolIndex",
        "UltrasharpTools.Tools.Services.CodeAnalysisService"
    )
    foreach ($fqn in $types) {
        Write-Host "Calling UltrasharpTool_ViewDefinition for $fqn..." -ForegroundColor Cyan
        Send-McpRequest -Process $process -Method "tools/call" -Params @{
            name = "UltrasharpTool_ViewDefinition"
            arguments = @{
                fullyQualifiedName = $fqn
            }
        }
        Start-Sleep -Milliseconds 500
    }

    # Call UltrasharpTool_GetMembers for several types
    foreach ($fqn in $types) {
        Write-Host "Calling UltrasharpTool_GetMembers for $fqn..." -ForegroundColor Cyan
        Send-McpRequest -Process $process -Method "tools/call" -Params @{
            name = "UltrasharpTool_GetMembers"
            arguments = @{
                fullyQualifiedName = $fqn
            }
        }
        Start-Sleep -Milliseconds 500
    }

    # Call SearchDefinitions
    Write-Host "Calling UltrasharpTool_SearchDefinitions..." -ForegroundColor Cyan
    Send-McpRequest -Process $process -Method "tools/call" -Params @{
        name = "UltrasharpTool_SearchDefinitions"
        arguments = @{
            pattern = "Service"
            caseSensitive = $false
            maxResults = 20
        }
    }
    Start-Sleep -Seconds 1

    Write-Host "✓ Training operations completed" -ForegroundColor Green
    Write-Host "Waiting for profile flush..." -ForegroundColor Gray
    Start-Sleep -Seconds 3

    # Graceful shutdown
    Write-Host "Closing connection..." -ForegroundColor Cyan
    $process.StandardInput.Close()

    # Wait for graceful exit
    $exited = $process.WaitForExit(5000)
    if (-not $exited) {
        Write-Host "Force closing server..." -ForegroundColor Yellow
        $process.Kill()
    }

    Write-Host "✓ Training completed successfully" -ForegroundColor Green
}
catch {
    Write-Error "Training failed: $_"
    if ($process -and -not $process.HasExited) {
        $process.Kill()
    }
    exit 1
}
finally {
    if ($process) {
        $process.Dispose()
    }
}
