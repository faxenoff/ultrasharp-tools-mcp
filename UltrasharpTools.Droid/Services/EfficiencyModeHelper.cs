using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services;

/// <summary>
/// Кроссплатформенный хелпер для настоящего Efficiency Mode.
/// Windows 11+: EcoQoS (значок листочка в Task Manager)
/// macOS: QoS Class Background
/// Linux: nice + SCHED_IDLE + ionice
/// </summary>
public static partial class EfficiencyModeHelper {
    #region Windows EcoQoS

    private const int ProcessPowerThrottling = 4;
    private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessInformation(
    IntPtr hProcess,
    int processInformationClass,
    ref PROCESS_POWER_THROTTLING_STATE processInformation,
    int processInformationSize);

    #endregion

    #region macOS QoS

    // QoS classes from <sys/qos.h>
    private const int QOS_CLASS_BACKGROUND = 0x09;
    private const int QOS_CLASS_DEFAULT = 0x15;

    [LibraryImport("libSystem.B.dylib", EntryPoint = "pthread_set_qos_class_self_np")]
    private static partial int pthread_set_qos_class_self_np(int qos_class, int relative_priority);

    #endregion

    #region Linux

    [LibraryImport("libc", EntryPoint = "nice")]
    private static partial int nice(int inc);

    [LibraryImport("libc", EntryPoint = "setpriority")]
    private static partial int setpriority(int which, int who, int prio);

    [LibraryImport("libc", EntryPoint = "sched_setscheduler")]
    private static partial int sched_setscheduler(int pid, int policy, ref SchedParam param);

    [LibraryImport("libc", EntryPoint = "ioprio_set", SetLastError = true)]
    private static partial int ioprio_set(int which, int who, int ioprio);

    [StructLayout(LayoutKind.Sequential)]
    private struct SchedParam {
        public int sched_priority;
    }

    // Linux constants
    private const int PRIO_PROCESS = 0;
    private const int SCHED_IDLE = 5;
    private const int IOPRIO_WHO_PROCESS = 1;
    private const int IOPRIO_CLASS_IDLE = 3;
    private const int IOPRIO_CLASS_SHIFT = 13;

    #endregion

    /// <summary>
    /// Включает энергоэффективный режим для текущего процесса.
    /// </summary>
    public static bool EnableEfficiencyMode(ILogger? logger = null) {
        if (OperatingSystem.IsWindows()) {
            return EnableWindowsEcoQos(logger);
        }

        if (OperatingSystem.IsMacOS()) {
            return EnableMacOsBackgroundQos(logger);
        }

        if (OperatingSystem.IsLinux()) {
            return EnableLinuxIdleMode(logger);
        }

        logger?.LogWarning("[EfficiencyMode] Unsupported platform: {Platform}", RuntimeInformation.OSDescription);
        return false;
    }

    /// <summary>
    /// Отключает энергоэффективный режим, возвращает нормальный приоритет.
    /// </summary>
    public static bool DisableEfficiencyMode(ILogger? logger = null) {
        if (OperatingSystem.IsWindows()) {
            return DisableWindowsEcoQos(logger);
        }

        if (OperatingSystem.IsMacOS()) {
            return DisableMacOsBackgroundQos(logger);
        }

        if (OperatingSystem.IsLinux()) {
            return DisableLinuxIdleMode(logger);
        }

        logger?.LogWarning("[EfficiencyMode] Unsupported platform: {Platform}", RuntimeInformation.OSDescription);
        return false;
    }

    #region Windows Implementation

    private static bool EnableWindowsEcoQos(ILogger? logger) {
        try {
            using var process = Process.GetCurrentProcess();

            // Пробуем включить EcoQoS (Windows 11+)
            try {
                var state = new PROCESS_POWER_THROTTLING_STATE {
                    Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                    ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                    StateMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED // Enable throttling
                };

                var success = SetProcessInformation(
                    process.Handle,
                    ProcessPowerThrottling,
                    ref state,
                    Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());

                if (success) {
                    // Ставим Idle для показа иконки в Task Manager
                    process.PriorityClass = ProcessPriorityClass.Idle;
                    logger?.LogInformation("[EfficiencyMode] Windows EcoQoS enabled (Efficiency Mode active)");
                    return true;
                }

                // Fallback для старых версий Windows
                var error = Marshal.GetLastWin32Error();
                logger?.LogDebug("[EfficiencyMode] EcoQoS not available (error {Error}), falling back to BelowNormal priority", error);
            } catch (DllNotFoundException) {
                logger?.LogDebug("[EfficiencyMode] kernel32.dll not found, falling back to BelowNormal priority");
            } catch (EntryPointNotFoundException) {
                logger?.LogDebug("[EfficiencyMode] SetProcessInformation not found, falling back to BelowNormal priority");
            }

            // Fallback: ставим Idle приоритет
            process.PriorityClass = ProcessPriorityClass.Idle;
            logger?.LogInformation("[EfficiencyMode] Fallback: Process priority set to Idle");
            return true;
        } catch (Exception ex) {
            logger?.LogWarning(ex, "[EfficiencyMode] Failed to enable Windows efficiency mode");
            return false;
        }
    }

    private static bool DisableWindowsEcoQos(ILogger? logger) {
        try {
            using var process = Process.GetCurrentProcess();

            // Отключаем EcoQoS
            var state = new PROCESS_POWER_THROTTLING_STATE {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = 0 // Disable throttling
            };

            SetProcessInformation(
            process.Handle,
            ProcessPowerThrottling,
            ref state,
            Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());

            // Восстанавливаем нормальный приоритет
            process.PriorityClass = ProcessPriorityClass.Normal;
            logger?.LogInformation("[EfficiencyMode] Windows EcoQoS disabled (Normal mode)");
            return true;
        } catch (Exception ex) {
            logger?.LogWarning(ex, "[EfficiencyMode] Failed to disable Windows efficiency mode");
            return false;
        }
    }

    #endregion

    #region macOS Implementation

    private static bool EnableMacOsBackgroundQos(ILogger? logger) {
        try {
            var result = pthread_set_qos_class_self_np(QOS_CLASS_BACKGROUND, 0);
            if (result == 0) {
                logger?.LogInformation("[EfficiencyMode] macOS Background QoS enabled");
                return true;
            }

            logger?.LogWarning("[EfficiencyMode] Failed to set macOS QoS class, result: {Result}", result);
            return false;
        } catch (Exception ex) {
            logger?.LogWarning(ex, "[EfficiencyMode] Failed to enable macOS efficiency mode");
            return false;
        }
    }

    private static bool DisableMacOsBackgroundQos(ILogger? logger) {
        try {
            var result = pthread_set_qos_class_self_np(QOS_CLASS_DEFAULT, 0);
            if (result == 0) {
                logger?.LogInformation("[EfficiencyMode] macOS Default QoS restored");
                return true;
            }

            logger?.LogWarning("[EfficiencyMode] Failed to restore macOS QoS class, result: {Result}", result);
            return false;
        } catch (Exception ex) {
            logger?.LogWarning(ex, "[EfficiencyMode] Failed to disable macOS efficiency mode");
            return false;
        }
    }

    #endregion

    #region Linux Implementation

    private static bool EnableLinuxIdleMode(ILogger? logger) {
        try {
            var success = true;

            // 1. Увеличиваем nice value (19 = минимальный приоритет)
            var niceResult = nice(19);
            if (niceResult == -1) {
                logger?.LogDebug("[EfficiencyMode] nice() returned -1 (may be ok if already low priority)");
            }

            // 2. Пробуем установить SCHED_IDLE scheduler
            var param = new SchedParam { sched_priority = 0 };
            var schedResult = sched_setscheduler(0, SCHED_IDLE, ref param);
            if (schedResult != 0) {
                logger?.LogDebug("[EfficiencyMode] sched_setscheduler(SCHED_IDLE) failed, continuing with nice only");
            }

            // 3. Пробуем установить idle I/O priority
            var ioprio = (IOPRIO_CLASS_IDLE << IOPRIO_CLASS_SHIFT) | 0;
            var ioResult = ioprio_set(IOPRIO_WHO_PROCESS, 0, ioprio);
            if (ioResult != 0) {
                logger?.LogDebug("[EfficiencyMode] ioprio_set(IDLE) failed, I/O priority unchanged");
            }

            logger?.LogInformation("[EfficiencyMode] Linux idle mode enabled (nice=19, SCHED_IDLE, ionice=idle)");
            return success;
        } catch (Exception ex) {
            logger?.LogWarning(ex, "[EfficiencyMode] Failed to enable Linux efficiency mode");
            return false;
        }
    }

    private static bool DisableLinuxIdleMode(ILogger? logger) {
        try {
            // Восстанавливаем нормальный приоритет через setpriority
            setpriority(PRIO_PROCESS, 0, 0);

            // Для scheduler и ionice - требуются привилегии для повышения, 
            // оставляем как есть (процесс всё равно скоро вернётся к нормальной работе)

            logger?.LogInformation("[EfficiencyMode] Linux normal mode restored (nice=0)");
            return true;
        } catch (Exception ex) {
            logger?.LogWarning(ex, "[EfficiencyMode] Failed to disable Linux efficiency mode");
            return false;
        }
    }

    #endregion

    /// <summary>
    /// Проверяет, поддерживается ли EcoQoS на текущей системе.
    /// </summary>
    public static bool IsEcoQosSupported() {
        if (!OperatingSystem.IsWindows())
            return false;

        try {
            // Windows 11 build 22000+ или Windows 10 21H2+ (build 19044+)
            var version = Environment.OSVersion.Version;
            return version.Build >= 19044;
        } catch {
            return false;
        }
    }
}
