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

.PHONY: help build test install clean
.DEFAULT_GOAL := help

help:
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'

build: ## Build the solution (debug)
	dotnet build src/

test: ## Run all tests
	dotnet test src/

install: ## Publish single-file binary to ~/.local/bin
	dotnet publish $(CLI) \
		-c Release \
		-r $(RID) \
		--self-contained false \
		-p:PublishSingleFile=true \
		-p:DebugType=none \
		-o $(INSTALL_DIR)
	@echo "Installed: $(INSTALL_DIR)/fms"

demo: install ## Install then run the build-operator sample mission end-to-end
	cd missions/build-operator && fms init && fms run

clean: ## Remove build artefacts (bin/ and obj/)
	dotnet clean src/
	find src/ -type d \( -name bin -o -name obj \) | xargs rm -rf
