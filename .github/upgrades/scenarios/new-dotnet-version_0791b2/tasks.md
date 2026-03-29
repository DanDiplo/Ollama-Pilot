# Ollama-Pilot .NET 10 Upgrade Tasks

## Overview

This document tracks the execution of the atomic upgrade of all projects in the repository to .NET 10. All project file updates and package updates will be performed simultaneously, followed by build and test validation and a single final commit.

**Progress**: 2/4 tasks complete (50%) ![0%](https://progress-bar.xyz/50)

---

## Tasks

### [✓] TASK-001: Verify prerequisites *(Completed: 2026-03-29 16:11)*
**References**: Plan §Implementation Timeline, Plan §Overview, Plan §Migration Strategy

- [✓] (1) Verify required .NET 10 SDK is installed on the execution environment per Plan §Implementation Timeline
- [✓] (2) Runtime/SDK version meets minimum requirements (**Verify**)
- [✓] (3) Check `global.json` (if present) for toolchain version locks and update compatibility notes per Plan §Implementation Timeline
- [✓] (4) Configuration files and common MSBuild imports (`Directory.Build.props`, `Directory.Packages.props`, any explicit `Import` entries) are compatible with the target framework per Plan §Project-by-Project Plans (**Verify**)

### [✓] TASK-002: Atomic framework and package upgrade with compilation fixes *(Completed: 2026-03-29 17:24)*
**References**: Plan §Implementation Timeline, Plan §Project-by-Project Plans, Plan §Package Update Reference, Plan §Breaking Changes Catalog

- [✓] (1) Update `TargetFramework`/target frameworks in all projects listed in Plan §Project-by-Project Plans to target .NET 10 per Plan §Implementation Timeline
- [⊘] (2) Update all NuGet package references per Plan §Package Update Reference (apply grouped/package-matrix updates referenced in the plan)
- [⊘] (3) Restore dependencies for the solution (`dotnet restore`) per Plan §Implementation Timeline
- [⊘] (4) Build the entire solution and fix all compilation errors caused by framework and package upgrades (reference Plan §Breaking Changes Catalog for known breaking-change fixes)
- [⊘] (5) Solution builds with 0 errors (**Verify**)

### [✗] TASK-003: Run test suites and validate upgrade
**References**: Plan §Testing & Validation Strategy, Plan §Project-by-Project Plans, Plan §Breaking Changes Catalog

- [✗] (1) Run all test projects listed in Plan §Testing & Validation Strategy (execute full automated test suite)
- [ ] (2) Fix any test failures (apply fixes referenced in Plan §Breaking Changes Catalog and project-by-project notes)
- [ ] (3) Re-run the test suite after fixes
- [ ] (4) All tests pass with 0 failures (**Verify**)

### [▶] TASK-004: Final commit
**References**: Plan §Source Control Strategy

- [▶] (1) Commit all remaining changes with message: "TASK-004: Complete upgrade to .NET 10.0"








