SHELL := /bin/bash

API_DIR := apps/api
WEB_DIR := apps/web
API_PROJECT := $(API_DIR)/DevDocSpace.Api
DATA_PROJECT := $(API_DIR)/DevDocSpace.Data
DEV_DIR := .dev
API_LOG := $(DEV_DIR)/api.log
WEB_LOG := $(DEV_DIR)/web.log
API_PID := $(DEV_DIR)/api.pid
WEB_PID := $(DEV_DIR)/web.pid

.DEFAULT_GOAL := help
.PHONY: help install install-api install-web \
	db-up db-down db-reset db-logs db-psql wait-for-db \
	migrate api api-watch api-build api-test \
	web web-build web-test web-lint web-typecheck web-e2e \
	dev stop status logs logs-api logs-web clean

help: ## Show this help
	@echo "DevDocSpace developer commands"
	@echo ""
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-15s\033[0m %s\n", $$1, $$2}'

## --- Setup ---------------------------------------------------------------

install: install-api install-web ## Restore/install dependencies for both apps

install-api: ## Restore .NET dependencies and local tools (dotnet-ef)
	cd $(API_DIR) && dotnet tool restore && dotnet restore

install-web: ## Install npm dependencies for the web app
	cd $(WEB_DIR) && npm install
	@if [ ! -f $(WEB_DIR)/.env.local ]; then \
		cp $(WEB_DIR)/.env.example $(WEB_DIR)/.env.local; \
		echo "Created $(WEB_DIR)/.env.local from .env.example - fill in NEXT_PUBLIC_FIREBASE_* before signing in."; \
	fi

## --- Database --------------------------------------------------------------

db-up: ## Start Postgres in Docker
	docker compose up -d postgres
	$(MAKE) wait-for-db

db-down: ## Stop Postgres (keeps data volume)
	docker compose stop postgres

db-reset: ## Stop Postgres and delete its data volume
	docker compose down -v postgres

db-logs: ## Tail Postgres logs
	docker compose logs -f postgres

db-psql: ## Open a psql shell against the local Postgres
	docker compose exec postgres psql -U devdocspace -d devdocspace

wait-for-db: ## Block until Postgres reports ready
	@echo "Waiting for Postgres..."
	@for i in $$(seq 1 30); do \
		docker compose exec -T postgres pg_isready -U devdocspace >/dev/null 2>&1 && echo "Postgres is ready." && exit 0; \
		sleep 1; \
	done; \
	echo "Postgres did not become ready in time." >&2; exit 1

## --- API (.NET) ------------------------------------------------------------

migrate: ## Apply pending EF Core migrations
	cd $(API_DIR) && dotnet ef database update --project DevDocSpace.Data --startup-project DevDocSpace.Api

api: db-up ## Run the API in the foreground (http://localhost:5080)
	cd $(API_DIR) && dotnet run --project DevDocSpace.Api

api-watch: db-up ## Run the API with hot reload
	cd $(API_DIR) && dotnet watch --project DevDocSpace.Api

api-build: ## Build the API solution
	cd $(API_DIR) && dotnet build

api-test: ## Run API tests (pass FILTER=... to target a class/method)
	cd $(API_DIR) && dotnet test $(if $(FILTER),--filter "$(FILTER)",)

## --- Web (Next.js) -----------------------------------------------------------

web: ## Run the web app in the foreground (http://localhost:3000)
	cd $(WEB_DIR) && npm run dev

web-build: ## Production build of the web app
	cd $(WEB_DIR) && npm run build

web-test: ## Run web unit tests (vitest)
	cd $(WEB_DIR) && npm test

web-lint: ## Lint the web app
	cd $(WEB_DIR) && npm run lint

web-typecheck: ## Typecheck the web app
	cd $(WEB_DIR) && npm run typecheck

web-e2e: ## Run Playwright smoke tests
	cd $(WEB_DIR) && npx playwright test

## --- Full local stack --------------------------------------------------------

dev: db-up ## Start db + api + web together in this terminal (Ctrl+C stops all)
	@mkdir -p $(DEV_DIR)
	@echo "Starting API and web app. Logs: $(API_LOG), $(WEB_LOG)"
	@dotnet run --project $(API_PROJECT) > $(API_LOG) 2>&1 & echo $$! > $(API_PID)
	@npm --prefix $(WEB_DIR) run dev > $(WEB_LOG) 2>&1 & echo $$! > $(WEB_PID)
	@trap '$(MAKE) stop' INT TERM EXIT; \
	echo "API:  http://localhost:5080"; \
	echo "Web:  http://localhost:3000"; \
	echo "Tailing logs (Ctrl+C to stop everything)..."; \
	tail -f $(API_LOG) $(WEB_LOG)

stop: ## Stop background api/web processes started by 'make dev'
	@if [ -f $(API_PID) ]; then kill $$(cat $(API_PID)) 2>/dev/null || true; rm -f $(API_PID); fi
	@if [ -f $(WEB_PID) ]; then kill $$(cat $(WEB_PID)) 2>/dev/null || true; rm -f $(WEB_PID); fi
	@pkill -f "dotnet.*DevDocSpace.Api" 2>/dev/null || true
	@pkill -f "next-server|next dev" 2>/dev/null || true
	@echo "Stopped api/web (db left running; use 'make db-down' to stop it too)."

status: ## Show whether db/api/web look like they're running
	@docker compose ps postgres 2>/dev/null | tail -n +2 | awk '{print "db:  " $$0}' || echo "db:  not running"
	@[ -f $(API_PID) ] && kill -0 $$(cat $(API_PID)) 2>/dev/null && echo "api: running (pid $$(cat $(API_PID)))" || echo "api: not running"
	@[ -f $(WEB_PID) ] && kill -0 $$(cat $(WEB_PID)) 2>/dev/null && echo "web: running (pid $$(cat $(WEB_PID)))" || echo "web: not running"

logs: logs-api logs-web ## Print the tail of both dev logs

logs-api: ## Tail the API dev log (from 'make dev')
	@test -f $(API_LOG) && tail -n 50 $(API_LOG) || echo "No API log yet; run 'make dev' first."

logs-web: ## Tail the web dev log (from 'make dev')
	@test -f $(WEB_LOG) && tail -n 50 $(WEB_LOG) || echo "No web log yet; run 'make dev' first."

## --- Cleanup ---------------------------------------------------------------

clean: stop ## Remove build artifacts, node_modules and dev logs
	rm -rf $(DEV_DIR)
	rm -rf $(WEB_DIR)/node_modules $(WEB_DIR)/.next
	find $(API_DIR) -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
