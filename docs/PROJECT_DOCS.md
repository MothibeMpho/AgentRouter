AgentRouter — Project documentation
===================================

Purpose
-------
AgentRouter coordinates or routes work between internal agent components. This document collects structure and contribution guidance for maintainers.

Architecture overview
---------------------
- AppRunner.cs: contains the application runner logic and is the current active file under development.
- Other modules: inspect the project folder for additional services and helpers.

Key files and locations
-----------------------
- Solution file: AgentRouter.slnx (root folder)
- Project file: AgentRouter.csproj (root folder)
- Entry point / runner: AppRunner.cs

Local development
-----------------
- Build: dotnet build
- Run: Use Visual Studio or `dotnet run` from the project folder (if configured)
- Tests: (add test instructions here when tests are present)

Recommended modernization checklist
----------------------------------
1. Inspect AgentRouter.csproj for non-SDK project format elements.
2. If conversion is needed, prepare a branch and run an SDK-style conversion step-by-step, verifying the build after each change.
3. Update NuGet packages to supported versions for .NET 8 and resolve compile-time/API changes.
4. Enable nullable reference types if desired and fix warnings across modified projects.
5. Add CI (GitHub Actions) to run restore/build/tests on push and PR.

How to document changes
-----------------------
- For any architecture or API changes, add a short note under docs/PROJECT_DOCS.md with date and author.
- Keep README.md up-to-date with build/run steps.

Next steps you may want me to do
--------------------------------
- Create a CONTRIBUTING.md and CODE_OF_CONDUCT.md
- Add a basic GitHub Actions workflow that builds on push
- Start a modernization scenario (sdk-style-conversion or dotnet-version-upgrade) for AgentRouter.csproj and produce a plan

