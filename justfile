# Fantasy Football RAG — task runner. Run `just` (or `just --list`) to see recipes.

# show available recipes
default:
    @just --list

# restore + build the solution
build:
    dotnet build

# provision Foundry (resource + gpt-5-mini + text-embedding-3-small)
provision:
    ./scripts/provision-foundry.sh

# verify the keyless model call works (W1 done-bar #2)
model:
    dotnet run --project src -- model

# load + parse the corpus and print counts (W1 done-bar #3); pass a PATH to override
load path="data/sample-corpus":
    dotnet run --project src -- load {{path}}

# build with debug symbols (Debug config) — what the editor debugger launches
build-debug:
    dotnet build -c Debug

# delete all Azure resources for this project
teardown:
    ./scripts/teardown-azure.sh
