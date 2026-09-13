AgentRouter — Project documentation
===================================

Purpose
-------
AgentRouter is an OpenAI-compatible router/proxy in front of LM Studio (or any
OpenAI-compatible server). It matches the last user message against rule-based
skills (`skills/*.md`) and forwards the request to the right model, streaming
the response back live. See README.md for user-facing setup; this file is the
maintainer reference.

Architecture overview
---------------------
- Program.cs: web host. `GET /health`, `GET /v1/models` (transparent proxy),
  `POST /v1/chat/completions` (rewrites `model`, injects system prompt, pipes
  SSE through). Config layering: `appsetting.json` -> `appsetting.Local.json`
  (git-ignored) -> environment variables.
- AppRunner.cs: skill matching (`SelectSkill`/`GetTargetSkill`), payload
  building (`BuildChatRequest`), upstream `HttpClient`, key resolution
  (`LmStudio:ApiKey` -> `LmStudio:Key` -> `LMSTUDIO_API_KEY`, first non-empty).
  Never logs or returns the key — only `HasApiKey` boolean.
- businessLogic/skills/SkillsLoader.cs: parses `skills/*.md` YAML front-matter
  (`name`, `trigger_keywords`, `model`, optional `base_url`) + markdown body
  as the system prompt. Returns skills sorted by name for determinism.
- models/: `Skill` (+ optional `BaseUrl` per-skill override), chat DTOs.
- skills/: routing rules. Match = case-insensitive substring on last user
  message; longest keyword wins, ties by skill name; no match -> DefaultModel.

Key files and locations
-----------------------
- Solution file: AgentRouter.slnx (root folder)
- Project file: AgentRouter.csproj (root folder)
- Entry point: Program.cs (web host); AppRunner.cs (routing core)
- Committed config template: appsetting.json (placeholders ONLY)
- Local secrets template: appsetting.Local.example.json -> copy to
  appsetting.Local.json (git-ignored, never commit)
- Env template: .env.example (scratch only — .NET does not auto-load .env)

Secrets management (maintainer contract)
----------------------------------------
- appsetting.json MUST stay secret-free. Real keys go ONLY in
  appsetting.Local.json (local dev) or env vars (`LmStudio__ApiKey`).
- .gitignore MUST keep blocking: appsetting.Local.json, appsettings.*.json,
  .env, secrets.json. Verify with `git ls-files | findstr appsetting`.
- Health/startup expose only `hasApiKey` boolean, never the value.
- If a key ever lands in git history: rotate it first, then purge with
  git filter-repo / BFG (a delete-commit alone does not remove it).

Local development
-----------------
- Restore: dotnet restore AgentRouter.slnx
- Build: dotnet build AgentRouter.slnx --configuration Release
- Run: dotnet run --project AgentRouter.csproj (listens on AgentRouter:BindAddress)
- Tests: dotnet test tests/AgentRouter.Tests/AgentRouter.Tests.csproj
- Secrets: copy appsetting.Local.example.json -> appsetting.Local.json and
  paste real key there, OR export $env:LmStudio__ApiKey (PowerShell) /
  export LmStudio__ApiKey (bash). Confirm via GET /health (hasApiKey: true).

How to document changes
-----------------------
- For any architecture or API changes, add a short note under docs/PROJECT_DOCS.md with date and author.
- Keep README.md up-to-date with build/run steps.

Changelog
---------
- 2026-09-13: layered secrets config (Local.json + env vars), secret-free
  committed template, HasApiKey health flag, full README rewrite.

