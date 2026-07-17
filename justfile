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

# provision Azure AI Search (Free tier, roles-only auth)
provision-search:
    ./scripts/provision-search.sh

# verify the keyless model call works (W1 done-bar #2)
model:
    dotnet run --project src -- model

# load + parse the corpus and print counts (W1 done-bar #3); pass a PATH to override
load path="data/sample-corpus":
    dotnet run --project src -- load {{path}}

# chunk + embed + index the corpus into Azure AI Search (W2); pass a PATH to override
ingest path="data/sample-corpus":
    dotnet run --project src -- ingest {{path}}

# query the RAG system with a question; pass mode and filter to override defaults:
#    just query "Which receivers had big games in week 14?" vector
query question mode="hybrid" filter="":
    dotnet run --project src -- query "{{question}}" {{mode}} "{{filter}}"

# build with debug symbols (Debug config) — what the editor debugger launches
build-debug:
    dotnet build -c Debug

# delete all Azure resources for this project
teardown:
    ./scripts/teardown-azure.sh
