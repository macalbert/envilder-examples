.PHONY: test test-typescript-testcontainers test-typescript-aspire test-python-testcontainers test-dotnet-testcontainers test-dotnet-aspire

# Runs sequentially on purpose: every example starts its own emulators on the same Docker daemon
test: test-typescript-testcontainers test-typescript-aspire test-python-testcontainers test-dotnet-testcontainers test-dotnet-aspire

test-typescript-testcontainers:
	cd typescript-testcontainers && npm install && npm test

test-typescript-aspire:
	cd typescript-aspire && npm install && npm test

test-python-testcontainers:
	cd python-testcontainers && uv run pytest

test-dotnet-testcontainers:
	dotnet test dotnet-testcontainers

test-dotnet-aspire:
	dotnet test dotnet-aspire/AppHost.Tests
