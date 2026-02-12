---
author: mikael
date: 2026-02-12 17:00:00
noindex: false
tag: [kusto, mcp, azure data explorer, ai, llm]
title: "Kusto MCP Enhanced: Config-Driven Kusto Tools for AI Agents"
meta_description: A config-driven MCP server for Azure Data Explorer that lets you declare Kusto query tools in YAML instead of code — and when you should use agent skills instead.
featured_image: header.png
type: post
table_of_contents: true
url: /kusto-mcp-enhanced
---

I recently built and open-sourced [kusto-mcp-enhanced](https://github.com/mikaelweave/kusto-mcp-enhanced), a config-driven [Model Context Protocol](https://modelcontextprotocol.io/) (MCP) server for Azure Data Explorer (Kusto). The idea is simple: declare your Kusto query tools in YAML instead of writing C# methods for each one.

This post covers what I built, why, and — honestly — when you should skip it and use an agent skill instead.

## The Problem

LLMs are great at reasoning but unreliable at writing correct KQL. They get column names wrong, forget time filters, use the wrong syntax, and occasionally try to run destructive commands like `.drop table`. If you're building an AI agent that queries Kusto, you need guardrails.

The typical approach is a single "run query" MCP tool and a prompt telling the LLM what tables exist. That works for exploration, but for the queries your team runs every day — the ones with hard-won knowledge about which columns to join, which paths to filter out, which time ranges make sense — you want something more structured.

## What kusto-mcp-enhanced Does

The server reads a YAML config file and dynamically registers MCP tools. No code changes needed to add, modify, or remove tools. The config declares:

- **Clusters** — connection URIs, databases, auth methods, timeouts
- **Tools** — each with a type, parameters, and query templates
- **Safety rules** — regex blocklist for `.set`, `.drop`, `.alter`, etc.

### Tool Types

**`templated`** tools are the core feature. They define parameterized KQL templates with Mustache-like syntax:

```yaml
tools:
  - name: search_errors
    type: templated
    description: "Search for application errors"
    cluster: my-logs
    parameters:
      - name: errorType
        type: enum
        default: "all"
        allowedValues: ["exceptions", "http", "all"]
      - name: hoursBack
        type: int
        default: 24
        validation:
          min: 1
          max: 168
    queries:
      - name: exceptions
        condition: "errorType == 'exceptions' || errorType == 'all'"
        kql: |
          AppEvents
          | where Timestamp >= ago({{hoursBack}}h)
          | where Level == "Error"
          | summarize Count=count() by ExceptionType
          | order by Count desc
```

The LLM sees a tool called `search_errors` with typed parameters. It never writes KQL — it just picks `errorType=http` and `hoursBack=6`. The server renders the template, validates safety, and executes it.

**`raw_query`** is the escape hatch — pass-through KQL execution with safety validation. For when the canned queries aren't enough.

**`schema_inspect`** returns table schema and a sample row, so the LLM can learn table structure before writing queries.

### What the Engine Does

Under the hood, the ~1,800 lines of C# implement six layers:

| Layer | Purpose |
|---|---|
| Template renderer | `{{param}}`, `{{#if}}...{{/if}}`, dot-notation into JSON |
| Computed parameters | `switch/case` that injects KQL clauses based on enum values |
| Condition evaluator | `&&`, `\|\|`, `==`, `!=` to conditionally include/exclude query blocks |
| Parameter validator | Type coercion, range/pattern/enum validation, `@now-24h` magic dates |
| Safety validator | Regex blocklist for dangerous Kusto commands |
| Result formatter | Markdown table or JSON output |

A single templated tool can run multiple conditional queries and merge results in one tool call. For example, a `search_errors` tool with `errorType=all` executes both an exceptions query and an HTTP errors query, then combines them.

## When to Use This vs. Agent Skills

After building all of this, I stepped back and asked: could a well-written agent skill replace the template engine?

### Where the MCP shim wins

**Deterministic query construction.** A templated tool produces the exact same KQL for the same parameters, every time. An LLM writing freeform KQL will drift — wrong column names, missing filters, inconsistent patterns.

**Safety at the transport layer.** The blocklist runs after rendering but before execution. It's a hard gate, not a prompt suggestion the LLM can reason its way around.

**Zero-code onboarding.** An ops team member adds a tool by editing YAML. No C#, no PR review for code changes, no redeployment.

**Multi-query orchestration.** One tool call can execute multiple queries conditionally and merge results. A skill would need multiple round-trips.

### Where agent skills win

**Reasoning over results.** The MCP shim returns raw tables. A skill can chain: query → analyze → decide what to query next → drill down. The shim has no feedback loop.

**Dynamic queries.** Templates are rigid. Novel questions need `raw_query`, which is functionally what a skill with schema context provides — but with more flexibility.

**No infrastructure.** A skill is a markdown file. This shim is a compiled C# application with NuGet dependencies and a runtime.

**Cross-tool workflows.** A skill naturally chains operations. The MCP shim exposes isolated tools.

### The Hybrid Approach

The strongest architecture uses both:

```
Agent Skill (reasoning + orchestration)
  ├── MCP shim for known patterns (templated tools)
  └── raw KQL for novel queries (with safety rules)
```

The skill handles "what should I look at next?" while the MCP shim handles "execute this specific, tested query correctly."

### The Simplest Viable MCP Server

If you lean skill-first, the minimal MCP server is just **three tools**: `run_query`, `get_schema`, and `list_tables` — with the safety blocklist. Move all domain knowledge into the skill prompt. The LLM already knows how to add `| where StatusCode >= 400` when asked about HTTP errors.

The templated tools are essentially **canned queries disguised as structured tools**. They're valuable when you want zero LLM discretion over query construction. But for most agent use cases where you want the LLM to reason, the YAML templates actively constrain it.

## Getting Started

```bash
# Clone
git clone https://github.com/mikaelweave/kusto-mcp-enhanced.git

# Build
dotnet restore KustoMcp/KustoMcp.csproj
dotnet build KustoMcp/KustoMcp.csproj

# Run with example config
dotnet run --project KustoMcp/KustoMcp.csproj -- --config KustoMcp/Examples/basic-errors.yaml
```

Config can be passed via `--config <path>`, the `KUSTO_MCP_CONFIG` environment variable, or by placing a `kusto-mcp-config.yaml` in the working directory. Both YAML and JSON formats are supported.

Check out the [repository](https://github.com/mikaelweave/kusto-mcp-enhanced) for the full example configs, including a multi-cluster setup with pagination and computed parameters.
