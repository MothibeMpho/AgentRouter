# AgentRouter

OpenAI-compatible router/proxy in front of **LM Studio** (or any OpenAI-compatible server).
It matches the last user message against rule-based **skills** and forwards the
request to the right model — streaming (SSE), temperatures, and other parameters
pass through untouched.

```
Client (Continue / curl / OpenAI SDK)
  -> http://localhost:5080/v1/chat/completions
  -> AgentRouter picks skill -> rewrites "model" -> injects system prompt
  -> LM Studio /v1/chat/completions (response streamed back live)
```

## Requirements

- .NET 8 SDK
- LM Studio running locally (default `http://127.0.0.1:1234`), or any OpenAI-compatible endpoint
- Visual Studio 2026+ or VS Code (optional)

## Quick start

1. Clone and restore:
   ```bash
   dotnet restore AgentRouter.slnx
   dotnet build AgentRouter.slnx --configuration Release
   ```
2. Start LM Studio and load your models (see [Skills](#skills--routing) below).
3. Run the router:
   ```bash
   dotnet run --project AgentRouter.csproj
   # listens on http://localhost:5080 by default
   ```
4. Try it:
   ```bash
   curl http://localhost:5080/health
   curl http://localhost:5080/v1/models
   curl http://localhost:5080/v1/chat/completions ^
     -H "Content-Type: application/json" ^
     -d "{\"messages\":[{\"role\":\"user\",\"content\":\"refactor this c# code\"}]}"
   ```
5. Point any OpenAI-compatible client at `http://localhost:5080`:
   Continue config (`~/.continue/config.yaml`) example:
   ```yaml
   models:
     - name: AgentRouter
       provider: openai
       model: qwen/qwen3.5-9b   # any name — router rewrites it per skill
       apiBase: http://localhost:5080
       apiKey: not-needed
   ```

## Skills / routing

Each file in `skills/*.md` is one routing rule. Front-matter drives the match,
body becomes the injected system prompt:

```md
---
name: code-helper
trigger_keywords: [debug, refactor, csharp, c#]
model: deepseek/deepseek-r1-0528-qwen3-8b
base_url: http://127.0.0.1:1234   # optional: per-skill LM Studio instance
---
Use this skill for code-related tasks...
```

- Matching: case-insensitive substring on the last user message.
  Longest keyword wins; ties break by skill name (deterministic).
- No match -> `AgentRouter:DefaultModel`.
- The skill description is prepended as a `system` message (skipped if the
  caller already sent one).
- Running models on different LM Studio ports? Add `base_url:` per skill.
  Same instance? Omit it — model-id routing is enough.

Current skills: `code-helper` (deepseek), `doc-write` (qwen), `reasoning-helper` (qwen).

## Configuration & API keys (read this before pushing to GitHub)

**Rule: `appsetting.json` is a committed placeholder template — never put a real
key in it.** Real keys live outside git and are layered at runtime:

| Priority | Source | Example | Committed? |
|---|---|---|---|
| 1 (highest) | Environment variables | `LmStudio__ApiKey=sk-...` | No |
| 2 | `appsetting.Local.json` | copy of `appsetting.Local.example.json` | **No (git-ignored)** |
| 3 (lowest) | `appsetting.json` | `ApiKey: ""` placeholder | Yes — safe |

Key resolution inside the app (`AppRunner`): `LmStudio:ApiKey` -> legacy
`LmStudio:Key` -> legacy `LMSTUDIO_API_KEY` env var. First non-empty wins.

Setup for local dev (pick ONE):

**Option A — local override file (easiest):**
```bash
copy appsetting.Local.example.json appsetting.Local.json   # Windows
# edit appsetting.Local.json, paste your real key
dotnet run --project AgentRouter.csproj
```

**Option B — environment variable (best for servers/CI):**
```powershell
# Windows PowerShell (current session only)
$env:LmStudio__ApiKey="sk-your-real-key"
dotnet run --project AgentRouter.csproj
```
```bash
# Linux/macOS
export LmStudio__ApiKey="sk-your-real-key"
dotnet run --project AgentRouter.csproj
```

`.env.example` is provided as a scratch template only — .NET does not auto-load
`.env`; export the vars yourself or use a tool that does.

Verify without leaking: `GET /health` returns `hasApiKey: true/false` (never the
value), and startup logs `API key configured: True/False` — the key itself is
never logged, returned, or forwarded to clients (only sent as a `Bearer` header
upstream to LM Studio).

LM Studio tip: local LM Studio usually needs **no key** — leave `ApiKey` empty.
You'll only need one if you point `BaseUrl` at a hosted provider later.

### GitHub safety checklist

- [ ] `.gitignore` blocks `appsetting.Local.json`, `appsettings.*.json`,
      `.env`, `secrets.json` (already done in this repo — don't remove).
- [ ] `git ls-files | findstr appsetting` shows only `appsetting.json` and
      `appsetting.Local.example.json` (both placeholders).
- [ ] Before first push: `git diff --cached` — confirm no real key staged.
- [ ] Accidentally committed a key? Rotate it immediately (old one is burned),
      then purge history (`git filter-repo` / BFG) — deleting the file in a new
      commit is NOT enough.

## Project layout

- `Program.cs` — web host: `/health`, `GET /v1/models`, `POST /v1/chat/completions`
- `AppRunner.cs` — skill matching, payload building, upstream `HttpClient`
- `businessLogic/skills/SkillsLoader.cs` — parses `skills/*.md` front-matter
- `models/` — `Skill`, chat DTOs
- `skills/` — routing rules (edit these to add models)
- `tests/AgentRouter.Tests/` — xUnit tests (`dotnet test`)
- `AgentRouter.slnx` — solution (app + tests)

## Build & test

```bash
dotnet build AgentRouter.slnx --configuration Release
dotnet test tests/AgentRouter.Tests/AgentRouter.Tests.csproj --configuration Release
```

## Contributing

- Open a PR describing the change
- Keep `appsetting.json` secret-free; run a local build with 0 new warnings

