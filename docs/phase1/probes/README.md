# Phase 1 Probes (AC4)

Probes demonstrating the 6 defects on base 707f86c.

## Usage (base worktree)

1. Copy the entire folder `Phase1.Probes/` to the base worktree:
   ```bash
   cp -r docs/phase1/probes/Phase1.Probes C:\Users\donoWin\AppData\Local\Temp\syne-base\syne\Phase1.Probes
   ```

2. Run tests from base worktree:
   ```bash
   cd C:\Users\donoWin\AppData\Local\Temp\syne-base\syne\Phase1.Probes
   dotnet test Phase1.Probes.csproj
   ```

3. Capture full output as evidence and commit it under `docs/phase1/probes/evidence/` (for example: `probe-results-707f86c.txt`).

**Important:** The probe project is NOT included in Syne.sln. It is intended to be run against base 707f86c to show each probe fails for the expected reason. It may not compile cleanly on the aligned branch (0.16.0) due to API changes. The source is preserved in the repository as the "probe" specified by AC4; captured outputs are the required proof.