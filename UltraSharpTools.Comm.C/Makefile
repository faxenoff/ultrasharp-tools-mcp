# UltraSharpTools.Comm - Makefile for Cosmopolitan Libc
#
# Prerequisites:
#   Download cosmocc from https://github.com/jart/cosmopolitan
#   Add to PATH or set COSMOCC variable
#
# Usage:
#   make           - Build portable APE binary (runs on Win/Linux/macOS)
#   make native    - Build native binary for current OS
#   make clean     - Remove build artifacts

COSMOCC ?= cosmocc
CC ?= gcc
CFLAGS = -Os -Wall -Wextra
SRC = comm.c

# Default: build portable APE binary
.PHONY: all
all: comm.com

# Portable binary using Cosmopolitan Libc
comm.com: $(SRC)
	$(COSMOCC) $(CFLAGS) -o $@ $<
	@echo "Built portable binary: comm.com"
	@echo "Run './comm.com --assimilate' to create native binary"

# Native binary for current OS
.PHONY: native
native: comm

comm: $(SRC)
ifeq ($(OS),Windows_NT)
	cl /O2 /Fe:comm.exe $(SRC) ws2_32.lib advapi32.lib
else
	$(CC) $(CFLAGS) -o $@ $<
endif

# Create native binaries for each platform (from APE)
.PHONY: assimilate
assimilate: comm.com
	./comm.com --assimilate
	@echo "Created native binary for current platform"

.PHONY: clean
clean:
	rm -f comm.com comm comm.exe *.o *.obj

.PHONY: help
help:
	@echo "UltraSharpTools.Comm Build System"
	@echo ""
	@echo "Targets:"
	@echo "  make          - Build portable APE binary (comm.com)"
	@echo "  make native   - Build native binary (comm/comm.exe)"
	@echo "  make clean    - Remove build artifacts"
	@echo ""
	@echo "The portable binary (comm.com) runs on:"
	@echo "  - Windows x64"
	@echo "  - Linux x64"
	@echo "  - macOS x64/ARM64"
	@echo "  - FreeBSD x64"
	@echo "  - NetBSD x64"
	@echo "  - OpenBSD x64"
