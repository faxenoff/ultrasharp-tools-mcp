#include <stdio.h>

int main() {
    printf("Platform detection test:\n");

    #ifdef _WIN32
    printf("  _WIN32: YES\n");
    #else
    printf("  _WIN32: NO\n");
    #endif

    #ifdef __COSMOPOLITAN__
    printf("  __COSMOPOLITAN__: YES\n");
    #else
    printf("  __COSMOPOLITAN__: NO\n");
    #endif

    #ifdef __linux__
    printf("  __linux__: YES\n");
    #else
    printf("  __linux__: NO\n");
    #endif

    #ifdef __APPLE__
    printf("  __APPLE__: YES\n");
    #else
    printf("  __APPLE__: NO\n");
    #endif

    return 0;
}
