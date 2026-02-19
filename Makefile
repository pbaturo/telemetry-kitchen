# Makefile for local dev workflow
.DEFAULT_GOAL := help

COMPOSE_FILE := infra/compose/docker-compose.yml
CONFIGURATION := Release
PUBLISH_DIR := artifacts/publish

.PHONY: help compose-up compose-down compose-build compose-logs compose-ps \
	dotnet-build dotnet-publish dotnet-clean clean docker-publish compose-rebuild

help: ## Show available targets
	@echo "Targets:";
	@echo "  help               Show available targets";
	@echo "  compose-up         Start containers (detached)";
	@echo "  compose-down       Stop and remove containers";
	@echo "  compose-build      Build Docker images";
	@echo "  compose-logs       Follow container logs";
	@echo "  compose-ps         Show container status";
	@echo "  dotnet-build       Build solution";
	@echo "  dotnet-publish     Publish solution to artifacts/publish";
	@echo "  dotnet-clean       Clean solution";
	@echo "  docker-publish     Publish .NET + build Docker images";
	@echo "  compose-rebuild    Rebuild images and restart containers"

compose-up: ## Start containers (detached)
	docker compose -f $(COMPOSE_FILE) up -d

compose-down: ## Stop and remove containers
	docker compose -f $(COMPOSE_FILE) down

compose-build: ## Build Docker images
	docker compose -f $(COMPOSE_FILE) build

compose-logs: ## Follow container logs
	docker compose -f $(COMPOSE_FILE) logs -f

compose-ps: ## Show container status
	docker compose -f $(COMPOSE_FILE) ps

dotnet-build: ## Build solution
	dotnet build telemetry-kitchen.sln -c $(CONFIGURATION)

dotnet-publish: ## Publish solution to artifacts/publish
	dotnet publish telemetry-kitchen.sln -c $(CONFIGURATION) -o $(PUBLISH_DIR)

dotnet-clean: ## Clean solution
	dotnet clean telemetry-kitchen.sln -c $(CONFIGURATION)

# Build local images after publishing .NET artifacts.
docker-publish: dotnet-publish compose-build ## Publish .NET + build Docker images

# Full local refresh: publish, build images, and recreate containers.
compose-rebuild: dotnet-publish compose-build ## Rebuild images and restart containers
	docker compose -f $(COMPOSE_FILE) up -d --force-recreate
