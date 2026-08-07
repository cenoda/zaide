.PHONY: run
## run (default) — launch the app
run:
	dotnet run --project src

.PHONY: build
## build — compile without running
build:
	dotnet build src

.PHONY: test
## test — run all tests
test:
	dotnet test

.PHONY: clean
## clean — remove build artifacts
clean:
	dotnet clean

.PHONY: restore
## restore — restore NuGet packages
restore:
	dotnet restore

.PHONY: check-theme-tokens
## check-theme-tokens — enforce zero hardcoded color literals in src/**/*.cs
check-theme-tokens:
	@bash scripts/check-theme-tokens.sh
