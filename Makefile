# Detect platform RID for single-file publish
UNAME_S := $(shell uname -s)
UNAME_M := $(shell uname -m)

ifeq ($(UNAME_S),Darwin)
  ifeq ($(UNAME_M),arm64)
    RID := osx-arm64
  else
    RID := osx-x64
  endif
else ifeq ($(UNAME_S),Linux)
  ifeq ($(UNAME_M),aarch64)
    RID := linux-arm64
  else
    RID := linux-x64
  endif
endif

INSTALL_DIR := $(HOME)/.local/bin
CLI         := src/ForgeMission.Cli

.PHONY: help build test install clean demo demo-naive demo-reliable dev-up dev-down dev-reset
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

clean: ## Remove build artefacts (bin/ and obj/)
	dotnet clean src/
	find src/ -type d \( -name bin -o -name obj \) | xargs rm -rf
