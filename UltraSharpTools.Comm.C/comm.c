// UltraSharpTools.Comm - Lightweight proxy for Droid MCP server
// Single portable binary for Windows/Linux/macOS via Cosmopolitan Libc
//
// Build with cosmocc:
//   cosmocc -Os -DNDEBUG -o UltraSharp-tools.com comm.c
//
// The binary auto-detects OS at runtime and uses:
//   Windows: Named Pipes (\\.\pipe\UltraSharpTools_Droid)
//   Unix: Unix Domain Sockets (/tmp/UltraSharpTools_Droid.sock)

#define _COSMO_SOURCE  // Enable IsWindows(), IsLinux(), etc.
#define NDEBUG 1       // Enable MS ABI thunks

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdbool.h>
#include <unistd.h>
#include <fcntl.h>
#include <errno.h>
#include <signal.h>
#include <sys/types.h>
#include <sys/stat.h>
#include <time.h>
#include <poll.h>
#include <sys/socket.h>
#include <sys/un.h>
#include <spawn.h>

// Cosmopolitan runtime detection
#include <libc/dce.h>

// Cosmopolitan NT API
#include <libc/nt/createfile.h>
#include <libc/nt/files.h>
#include <libc/nt/runtime.h>
#include <libc/nt/synchronization.h>
#include <libc/nt/process.h>
#include <libc/nt/ipc.h>
#include <libc/nt/enum/accessmask.h>
#include <libc/nt/enum/creationdisposition.h>
#include <libc/nt/enum/fileflagandattributes.h>
#include <libc/nt/enum/processcreationflags.h>
#include <libc/nt/enum/startf.h>
#include <libc/nt/struct/startupinfo.h>
#include <libc/nt/struct/processinformation.h>

#define VERSION "3.5.0"
#define APP_NAME "UltraSharpTools.Comm"
#define BUFFER_SIZE 8192
#define CONNECT_TIMEOUT_MS 2000
#define STARTUP_TIMEOUT_MS 30000

#define PIPE_PATH_WIN "\\\\.\\pipe\\UltraSharpTools_Droid"
#define PIPE_PATH_UNIX "/tmp/UltraSharpTools_Droid.sock"

static volatile int g_running = 1;

static void signal_handler(int sig) {
    (void)sig;
    g_running = 0;
}

static void print_help(void) {
    printf("%s v%s\n", APP_NAME, VERSION);
    printf("Lightweight proxy for UltraSharpTools MCP server.\n");
    printf("Connects to Droid via Named Pipe (starts Droid if not running).\n");
}

static void print_version(void) {
    printf("%s v%s\n", APP_NAME, VERSION);
}

static long get_time_ms(void) {
    struct timespec ts;
    clock_gettime(CLOCK_MONOTONIC, &ts);
    return ts.tv_sec * 1000 + ts.tv_nsec / 1000000;
}

// ============================================================================
// Windows implementation using Cosmopolitan NT API
// ============================================================================

// Convert ASCII to UTF-16 (simple, ASCII only)
static void ascii_to_utf16(const char *src, char16_t *dst, size_t dst_size) {
    size_t i;
    for (i = 0; i < dst_size - 1 && src[i]; i++) {
        dst[i] = (char16_t)(unsigned char)src[i];
    }
    dst[i] = 0;
}

static int64_t win_try_connect_pipe(void) {
    char16_t pipe_path_w[256];
    ascii_to_utf16(PIPE_PATH_WIN, pipe_path_w, 256);

    int64_t handle = CreateFile(
        pipe_path_w,
        kNtGenericRead | kNtGenericWrite,
        0,
        NULL,
        kNtOpenExisting,
        0,  // No overlapped for simplicity
        0
    );
    return handle;
}

static int win_get_exe_dir(char *buf, size_t buf_size) {
    // Use Cosmopolitan's GetProgramExecutableName
    char *exe = GetProgramExecutableName();
    if (!exe || !*exe) {
        strcpy(buf, ".");
        return -1;
    }

    strncpy(buf, exe, buf_size - 1);
    buf[buf_size - 1] = '\0';

    // Convert /D/path to D:\path (Cosmopolitan returns Unix-style paths)
    if (buf[0] == '/' && buf[1] && buf[2] == '/') {
        char drive = buf[1];
        buf[0] = drive;
        buf[1] = ':';
        // buf is now "D:/path..." - slashes will be fixed below
    }

    // Normalize to backslashes for Windows
    for (char *p = buf; *p; p++) {
        if (*p == '/') *p = '\\';
    }

    // Find last backslash
    char *last = strrchr(buf, '\\');
    if (last) *last = '\0';

    return 0;
}

static int win_start_droid(int argc, char **argv) {
    char exe_path[1024];
    char droid_path[1100];
    char cmd_line[4096];
    char16_t cmd_line_w[4096];

    win_get_exe_dir(exe_path, sizeof(exe_path));

    // Try same directory first
    snprintf(droid_path, sizeof(droid_path), "%s\\UltrasharpTools.Droid.exe", exe_path);

    // Check if exists, try sibling folder
    if (access(droid_path, F_OK) != 0) {
        snprintf(droid_path, sizeof(droid_path), "%s\\..\\Droid\\UltrasharpTools.Droid.exe", exe_path);
    }

    // Build command line
    snprintf(cmd_line, sizeof(cmd_line), "\"%s\" --pipe-server", droid_path);
    for (int i = 1; i < argc; i++) {
        strcat(cmd_line, " ");

        // Convert /D/path to D:\path for Windows if needed
        char arg_buf[1024];
        const char *arg = argv[i];
        if (arg[0] == '/' && arg[1] && arg[2] == '/') {
            snprintf(arg_buf, sizeof(arg_buf), "%c:%s", arg[1], arg + 2);
            // Convert slashes
            for (char *p = arg_buf; *p; p++) {
                if (*p == '/') *p = '\\';
            }
            arg = arg_buf;
        }

        // Only quote if contains spaces
        if (strchr(arg, ' ') != NULL) {
            strcat(cmd_line, "\"");
            strcat(cmd_line, arg);
            strcat(cmd_line, "\"");
        } else {
            strcat(cmd_line, arg);
        }
    }

    // Convert to UTF-16
    ascii_to_utf16(cmd_line, cmd_line_w, sizeof(cmd_line_w) / sizeof(cmd_line_w[0]));

    struct NtStartupInfo si = {0};
    struct NtProcessInformation pi = {0};

    si.cb = sizeof(si);
    si.dwFlags = kNtStartfUseshowwindow;
    si.wShowWindow = 0; // SW_HIDE

    bool32 ok = CreateProcess(
        NULL,
        cmd_line_w,
        NULL,
        NULL,
        false,
        kNtCreateNoWindow | kNtDetachedProcess,
        NULL,
        NULL,
        &si,
        &pi
    );

    if (!ok) return -1;

    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return 0;
}

static int win_run_proxy(int64_t pipe) {
    char buf[BUFFER_SIZE];
    int64_t stdin_h = GetStdHandle(kNtStdInputHandle);
    int64_t stdout_h = GetStdHandle(kNtStdOutputHandle);

    uint32_t bytes_read, bytes_written;
    uint32_t bytes_avail;

    while (g_running) {
        // Check if data available on pipe
        if (PeekNamedPipe(pipe, NULL, 0, NULL, &bytes_avail, NULL) && bytes_avail > 0) {
            if (ReadFile(pipe, buf, BUFFER_SIZE, &bytes_read, NULL) && bytes_read > 0) {
                WriteFile(stdout_h, buf, bytes_read, &bytes_written, NULL);
            }
        }

        // Check if data available on stdin
        if (PeekNamedPipe(stdin_h, NULL, 0, NULL, &bytes_avail, NULL) && bytes_avail > 0) {
            if (ReadFile(stdin_h, buf, BUFFER_SIZE, &bytes_read, NULL) && bytes_read > 0) {
                WriteFile(pipe, buf, bytes_read, &bytes_written, NULL);
            }
        }

        Sleep(10);
    }

    return 0;
}

static int win_main(int argc, char **argv) {
    if (argc > 1) {
        if (strcmp(argv[1], "--help") == 0 || strcmp(argv[1], "-h") == 0) {
            print_help();
            return 0;
        }
        if (strcmp(argv[1], "--version") == 0 || strcmp(argv[1], "-v") == 0) {
            print_version();
            return 0;
        }
    }

    int64_t pipe = win_try_connect_pipe();

    if (pipe == -1) {
        if (win_start_droid(argc, argv) != 0) {
            fprintf(stderr, "Failed to start Droid\n");
            return 1;
        }

        long start = get_time_ms();
        while (get_time_ms() - start < STARTUP_TIMEOUT_MS) {
            pipe = win_try_connect_pipe();
            if (pipe != -1) break;
            Sleep(200);
        }

        if (pipe == -1) {
            fprintf(stderr, "Timeout waiting for Droid\n");
            return 1;
        }
    }

    int result = win_run_proxy(pipe);
    CloseHandle(pipe);
    return result;
}

// ============================================================================
// Unix implementation
// ============================================================================

static int unix_try_connect_socket(void) {
    int sock = socket(AF_UNIX, SOCK_STREAM, 0);
    if (sock < 0) return -1;

    struct sockaddr_un addr = {0};
    addr.sun_family = AF_UNIX;
    strncpy(addr.sun_path, PIPE_PATH_UNIX, sizeof(addr.sun_path) - 1);

    if (connect(sock, (struct sockaddr*)&addr, sizeof(addr)) < 0) {
        close(sock);
        return -1;
    }

    return sock;
}

static int unix_get_exe_dir(char *buf, size_t buf_size) {
    // Use Cosmopolitan's GetProgramExecutableName
    char *exe = GetProgramExecutableName();
    if (!exe || !*exe) {
        ssize_t len = readlink("/proc/self/exe", buf, buf_size - 1);
        if (len < 0) {
            strcpy(buf, ".");
            return -1;
        }
        buf[len] = '\0';
    } else {
        strncpy(buf, exe, buf_size - 1);
        buf[buf_size - 1] = '\0';
    }

    char *last_slash = strrchr(buf, '/');
    if (last_slash) *last_slash = '\0';
    return 0;
}

static int unix_start_droid(int argc, char **argv) {
    char exe_path[2048];
    char droid_path[2200];

    unix_get_exe_dir(exe_path, sizeof(exe_path));

    snprintf(droid_path, sizeof(droid_path), "%s/UltrasharpTools.Droid", exe_path);

    if (access(droid_path, X_OK) != 0) {
        snprintf(droid_path, sizeof(droid_path), "%s/../Droid/UltrasharpTools.Droid", exe_path);
    }

    pid_t pid = fork();
    if (pid < 0) return -1;

    if (pid == 0) {
        setsid();
        close(STDIN_FILENO);
        close(STDOUT_FILENO);
        close(STDERR_FILENO);

        char **new_argv = malloc((argc + 3) * sizeof(char*));
        new_argv[0] = droid_path;
        new_argv[1] = "--pipe-server";
        for (int i = 1; i < argc; i++) {
            new_argv[i + 1] = argv[i];
        }
        new_argv[argc + 1] = NULL;

        execv(droid_path, new_argv);
        _exit(1);
    }

    return 0;
}

static int unix_run_proxy(int sock) {
    char stdin_buf[BUFFER_SIZE];
    char sock_buf[BUFFER_SIZE];

    fcntl(STDIN_FILENO, F_SETFL, O_NONBLOCK);
    fcntl(sock, F_SETFL, O_NONBLOCK);

    struct pollfd fds[2] = {
        { .fd = STDIN_FILENO, .events = POLLIN },
        { .fd = sock, .events = POLLIN }
    };

    while (g_running) {
        int ready = poll(fds, 2, 100);
        if (ready < 0) {
            if (errno == EINTR) continue;
            break;
        }
        if (ready == 0) continue;

        if (fds[0].revents & POLLIN) {
            ssize_t n = read(STDIN_FILENO, stdin_buf, BUFFER_SIZE);
            if (n <= 0) break;
            if (write(sock, stdin_buf, n) < 0) break;
        }

        if (fds[1].revents & POLLIN) {
            ssize_t n = read(sock, sock_buf, BUFFER_SIZE);
            if (n <= 0) break;
            if (write(STDOUT_FILENO, sock_buf, n) < 0) break;
        }

        if ((fds[0].revents | fds[1].revents) & (POLLHUP | POLLERR)) {
            break;
        }
    }

    return 0;
}

static int unix_main(int argc, char **argv) {
    signal(SIGINT, signal_handler);
    signal(SIGTERM, signal_handler);
    signal(SIGPIPE, SIG_IGN);

    if (argc > 1) {
        if (strcmp(argv[1], "--help") == 0 || strcmp(argv[1], "-h") == 0) {
            print_help();
            return 0;
        }
        if (strcmp(argv[1], "--version") == 0 || strcmp(argv[1], "-v") == 0) {
            print_version();
            return 0;
        }
    }

    int sock = unix_try_connect_socket();

    if (sock < 0) {
        if (unix_start_droid(argc, argv) != 0) {
            fprintf(stderr, "Failed to start Droid\n");
            return 1;
        }

        long start = get_time_ms();
        while (get_time_ms() - start < STARTUP_TIMEOUT_MS) {
            sock = unix_try_connect_socket();
            if (sock >= 0) break;
            usleep(200000);
        }

        if (sock < 0) {
            fprintf(stderr, "Timeout waiting for Droid\n");
            return 1;
        }
    }

    int result = unix_run_proxy(sock);
    close(sock);
    return result;
}

// ============================================================================
// Main - runtime OS detection
// ============================================================================

int main(int argc, char **argv) {
    if (IsWindows()) {
        return win_main(argc, argv);
    } else {
        return unix_main(argc, argv);
    }
}
