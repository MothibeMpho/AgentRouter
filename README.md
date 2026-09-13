AgentRouter
===========

Short description
-----------------
AgentRouter is a small .NET application in this repository. This README provides a quick starting point to build, run, and contribute.

Requirements
------------
- Visual Studio 2026 (Community) or later
- .NET 8 SDK

Quick start
-----------
1. Open the solution: AgentRouter.slnx
2. Build the solution in Visual Studio or run dotnet build from the solution folder.

Project layout
--------------
- Solution: AgentRouter.slnx
- Main project: AgentRouter.csproj
- Key source files: AppRunner.cs (entry/runner), other files under the project folder

Modernization notes
-------------------
This repository currently targets .NET 8. If you plan further modernization, common tasks include:
- Converting legacy csproj files to SDK-style (if applicable)
- Updating NuGet packages and resolving breaking changes
- Enabling nullable reference types and addressing warnings
- Adding CI builds that restore, build, and run tests

Contributing
------------
- Open a PR describing the change
- Run a local build and ensure no new warnings are introduced

Contact
-------
For questions, open an issue or create a PR with details.
