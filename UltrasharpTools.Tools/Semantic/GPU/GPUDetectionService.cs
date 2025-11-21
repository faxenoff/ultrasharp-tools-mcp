using System.Diagnostics;

namespace UltrasharpTools.Tools.Semantic.GPU;

/// <summary>
/// GPU detection service using nvidia-smi
/// </summary>
public sealed class GPUDetectionService : IGPUDetectionService
{
    private readonly ILogger<GPUDetectionService> _logger;
    private GPUInfo? _cachedInfo;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public GPUDetectionService(ILogger<GPUDetectionService> logger)
    {
        _logger = logger;
    }

    public async Task<GPUInfo> DetectAsync(CancellationToken cancellationToken = default)
    {
        // Return cached result if available
        if (_cachedInfo != null)
        {
            _logger.LogDebug("[GPUDetection] Using cached GPU info");
            return _cachedInfo;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cachedInfo != null)
            {
                return _cachedInfo;
            }

            _logger.LogInformation("[GPUDetection] Detecting GPU capabilities...");

            var info = new GPUInfo();

            // Try CUDA detection via nvidia-smi
            var cudaInfo = await DetectCudaAsync(cancellationToken);
            if (cudaInfo != null)
            {
                // Detect CUDA Toolkit version
                var cudaVersion = await DetectCudaVersionAsync(cancellationToken);
                var cudnnAvailable = await DetectCudnnAsync(cancellationToken);

                info = new GPUInfo
                {
                    Vendor = cudaInfo.Vendor,
                    Model = cudaInfo.Model,
                    ComputeCapability = cudaInfo.ComputeCapability,
                    MemoryMB = cudaInfo.MemoryMB,
                    CudaAvailable = cudaInfo.CudaAvailable,
                    CudaVersion = cudaVersion,
                    CudnnAvailable = cudnnAvailable,
                    ClockRateMHz = cudaInfo.ClockRateMHz,
                };

                _logger.LogInformation("[GPUDetection] CUDA GPU detected: {Info}", info);
            }
            else
            {
                _logger.LogDebug("[GPUDetection] No CUDA GPU detected");
                info = new GPUInfo
                {
                    Vendor = "unknown",
                    Model = "No GPU detected",
                    MemoryMB = 0,
                    CudaAvailable = false,
                };
            }

            _cachedInfo = info;
            return info;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void ClearCache()
    {
        _cachedInfo = null;
        _logger.LogDebug("[GPUDetection] Cache cleared");
    }

    private async Task<GPUInfo?> DetectCudaAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Run nvidia-smi to query GPU info
            // Format: name, compute_cap, memory.total (MB)
            var output = await RunProcessAsync(
                "nvidia-smi",
                "--query-gpu=name,compute_cap,memory.total --format=csv,noheader,nounits",
                timeoutMs: 5000,
                cancellationToken
            );

            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            // Parse output: "GeForce GTX 1650 Ti, 7.5, 4096"
            var parts = output.Trim().Split(',');
            if (parts.Length < 3)
            {
                _logger.LogWarning(
                    "[GPUDetection] Unexpected nvidia-smi output format: {Output}",
                    output
                );
                return null;
            }

            var model = parts[0].Trim();
            var computeCapStr = parts[1].Trim();
            var memoryStr = parts[2].Trim();

            if (!float.TryParse(computeCapStr, out var computeCap))
            {
                _logger.LogWarning(
                    "[GPUDetection] Failed to parse compute capability: {Value}",
                    computeCapStr
                );
                computeCap = 0;
            }

            if (!int.TryParse(memoryStr, out var memoryMB))
            {
                _logger.LogWarning("[GPUDetection] Failed to parse memory: {Value}", memoryStr);
                memoryMB = 0;
            }

            return new GPUInfo
            {
                Vendor = "nvidia",
                Model = model,
                ComputeCapability = computeCap,
                MemoryMB = memoryMB,
                CudaAvailable = true,
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[GPUDetection] nvidia-smi failed (CUDA not available)");
            return null;
        }
    }

    private async Task<string> RunProcessAsync(
        string fileName,
        string arguments,
        int timeoutMs,
        CancellationToken cancellationToken
    )
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        var outputBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
            throw new TimeoutException($"Process {fileName} timed out after {timeoutMs}ms");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Process {fileName} exited with code {process.ExitCode}"
            );
        }

        return outputBuilder.ToString();
    }

    /// <summary>
    /// Detect CUDA Toolkit version via nvcc or nvidia-smi
    /// </summary>
    private async Task<string?> DetectCudaVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Try nvcc first (CUDA Toolkit compiler)
            var nvccOutput = await RunProcessAsync(
                "nvcc",
                "--version",
                timeoutMs: 3000,
                cancellationToken
            );

            // Parse: "Cuda compilation tools, release 13.0, V13.0.76"
            var match = System.Text.RegularExpressions.Regex.Match(
                nvccOutput,
                @"release\s+([\d.]+)"
            );
            if (match.Success)
            {
                _logger.LogDebug(
                    "[GPUDetection] CUDA Toolkit version from nvcc: {Version}",
                    match.Groups[1].Value
                );
                return match.Groups[1].Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[GPUDetection] nvcc not available, trying nvidia-smi");
        }

        try
        {
            // Fallback: try nvidia-smi for CUDA Driver version
            var smiOutput = await RunProcessAsync(
                "nvidia-smi",
                "--query-gpu=driver_version --format=csv,noheader,nounits",
                timeoutMs: 3000,
                cancellationToken
            );

            var driverVersion = smiOutput.Trim();
            if (!string.IsNullOrWhiteSpace(driverVersion))
            {
                _logger.LogDebug(
                    "[GPUDetection] CUDA Driver version from nvidia-smi: {Version}",
                    driverVersion
                );
                // Note: This is driver version, not toolkit version, but still useful
                return $"{driverVersion} (driver)";
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[GPUDetection] Failed to detect CUDA version");
        }

        return null;
    }

    /// <summary>
    /// Detect if cuDNN is available (heuristic: check if nvidia-smi shows GPU with CUDA)
    /// </summary>
    private async Task<bool> DetectCudnnAsync(CancellationToken cancellationToken)
    {
        try
        {
            // cuDNN is typically installed with deep learning frameworks
            // We can't directly detect it without running code, so we use a heuristic:
            // If CUDA Toolkit is installed (nvcc available), cuDNN is likely also installed

            var nvccOutput = await RunProcessAsync(
                "nvcc",
                "--version",
                timeoutMs: 2000,
                cancellationToken
            );

            // If nvcc is available, assume cuDNN might be available too
            // (This is a heuristic - actual detection would require loading cuDNN library)
            return !string.IsNullOrWhiteSpace(nvccOutput);
        }
        catch
        {
            return false;
        }
    }
}
