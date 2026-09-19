# Detect platform RID for single-file publish
UNAME_S := $(shell uname -s)
UNAME_M := $(shell uname -m)
DESKTOP_HOST_PUBLISH_OPTIONS :=

ifeq ($(UNAME_S),Darwin)
  ifeq ($(UNAME_M),arm64)
    RID := osx-arm64
    DESKTOP_HOST_RID := maccatalyst-arm64
  else
    RID := osx-x64
    DESKTOP_HOST_RID := maccatalyst-x64
  endif
  DESKTOP_HOST_TFM := net10.0-maccatalyst
  DESKTOP_HOST_PUBLISH_OPTIONS := -p:CreatePackage=false
  DESKTOP_HOST_APP = $(DESKTOP_HOST)/bin/Release/$(DESKTOP_HOST_TFM)/$(DESKTOP_HOST_RID)/Forge.app
else ifeq ($(UNAME_S),Linux)
  ifeq ($(UNAME_M),aarch64)
    RID := linux-arm64
  else
    RID := linux-x64
  endif
endif

ifeq ($(OS),Windows_NT)
  RID := win-arm64
  DESKTOP_HOST_TFM := net10.0-windows10.0.19041.0
  DESKTOP_HOST_RID := win-arm64
  SHELL := bash
endif

INSTALL_DIR := $(HOME)/.local/bin
CLI         := src/ForgeMission.Cli
APPLICATION_HOST := src/ForgeMission.Application.Host
DESKTOP_SUPERVISOR := src/ForgeMission.Desktop
DESKTOP_HOST := src/ForgeMission.Desktop.Host
DESKTOP_DIR := dist/forge-desktop
DESKTOP_SUPERVISOR_EXE := $(DESKTOP_DIR)/ForgeMission.Desktop.exe
NORMALIZE_AOT_PE := pwsh -NoProfile -File ./scripts/Normalize-AotPeTimestamps.ps1

.PHONY: help build test install clean demo demo-naive demo-reliable dev-up dev-down dev-reset desktop desktop-publish
.DEFAULT_GOAL := help

help:
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'

build: ## Build the solution (debug)
	dotnet build src/

test: ## Run all tests
	dotnet test src/

install: ## Publish native AOT binary to ~/.local/bin
	dotnet publish $(CLI) \
		-c Release \
		-r $(RID) \
		-o $(INSTALL_DIR)
	@echo "Installed: $(INSTALL_DIR)/forge"

demo: install ## Install then run the build-operator sample mission end-to-end
	cd missions/build-operator && forge init && forge run

demo-naive: ## Run the one-shot loop demo — no retry, raw first-attempt output (requires forge in PATH)
	cd missions/loop-demo-naive && forge run

demo-reliable: ## Run the loop demo — retries until quality passes, shows convergence (requires forge in PATH)
	cd missions/loop-demo && forge run --steps

build-linux: ## Build linux-x64 binary into repo root (needed for docker build)
	dotnet publish $(CLI) -c Release -r linux-x64 -o . --self-contained
	@echo "forge-linux-x64 ready"

dev-up: ## Start local dev environment (Postgres + migrations)
	./scripts/dev-up.sh

dev-down: ## Stop local dev environment (keeps data)
	./scripts/dev-down.sh

dev-reset: ## Reset local dev environment (drops data volume, re-initialises)
	./scripts/dev-reset.sh

desktop-publish: ## Publish the desktop app (Application Host + supervisor + native MAUI host) as one self-contained folder
	@test -n "$(DESKTOP_HOST_TFM)" || (echo "desktop-publish is supported on Windows and macOS only." >&2; exit 1)
	dotnet workload restore $(DESKTOP_HOST)/ForgeMission.Desktop.Host.csproj
	rm -rf $(DESKTOP_DIR)
	dotnet publish $(APPLICATION_HOST) -c Release -r $(RID) --self-contained -o $(DESKTOP_DIR)
	dotnet publish $(DESKTOP_SUPERVISOR) -c Release -r $(RID) --self-contained -o $(DESKTOP_DIR)
	# Last on purpose: publishing into a shared folder prunes files a project published before but
	# no longer owns; keep the native shell last so its framework assets are present in the bundle.
	dotnet publish $(DESKTOP_HOST) -c Release -f $(DESKTOP_HOST_TFM) -r $(DESKTOP_HOST_RID) --self-contained $(DESKTOP_HOST_PUBLISH_OPTIONS) -o $(DESKTOP_DIR)

ifeq ($(UNAME_S),Darwin)
	@test -d "$(DESKTOP_HOST_APP)" || (echo "Expected macOS MAUI app bundle was not produced: $(DESKTOP_HOST_APP)" >&2; exit 1)
	ditto "$(DESKTOP_HOST_APP)" "$(DESKTOP_DIR)/Forge.app"
endif

ifeq ($(OS),Windows_NT)
	$(NORMALIZE_AOT_PE) -Image $(DESKTOP_SUPERVISOR_EXE)
	@identity_dir=$$(mktemp -d); \
	trap 'rm -rf -- "$$identity_dir"' EXIT; \
	dotnet publish $(DESKTOP_SUPERVISOR) -c Release -r $(RID) --self-contained -o "$$identity_dir"; \
	$(NORMALIZE_AOT_PE) -Image "$$identity_dir/ForgeMission.Desktop.exe"; \
	if ! cmp -s $(DESKTOP_SUPERVISOR_EXE) "$$identity_dir/ForgeMission.Desktop.exe"; then \
		echo "Normalized Supervisor differs from an isolated repeat publish." >&2; \
		exit 1; \
	fi; \
	echo "Windows Supervisor identity verified against an isolated repeat publish."
endif
	@echo "Desktop app published: $(DESKTOP_DIR)/ForgeMission.Desktop"

desktop: desktop-publish ## Publish the desktop app without launching it

clean: ## Remove all build artefacts (bin/, obj/, dist/)
	dotnet clean src/
	find src/ -type d \( -name bin -o -name obj \) | xargs rm -rf
	rm -rf dist/
