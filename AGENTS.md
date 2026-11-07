# HoneyDrunk.Memory agent instructions

This repository holds the HoneyDrunk agent-memory node (ADR-0022): `HoneyDrunk.Memory.Abstractions`, the `HoneyDrunk.Memory` runtime and `HoneyDrunk.Memory.Providers.InMemory`, in `HoneyDrunk.Memory.slnx`. Inspect [README.md](README.md), the tracked files and open implementation PRs before making claims; an open PR is not merged behavior. Similarity search, write-side embeddings and production providers are not implemented yet.

Build and test:

```bash
dotnet build HoneyDrunk.Memory.slnx -c Release
dotnet test HoneyDrunk.Memory.slnx -c Release
```

The contract-shape canary (`api/HoneyDrunk.Memory.Abstractions.contract-shape.txt`) fails on any change to the frozen Abstractions surfaces. To regenerate it after an intended change, first raise `Version` in `Directory.Build.props`, then run:

```bash
HONEYDRUNK_UPDATE_CONTRACT_SHAPE=1 dotnet test tests/HoneyDrunk.Memory.Abstractions.Tests -c Release --filter Category=ContractShape
```

Read the [shared engineering conventions](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards/blob/main/HoneyDrunk.Standards/docs/CONVENTIONS.md) and this repository's owning documentation before editing. Apply the parts relevant to this stack; preserve existing public contracts, dependency direction and repository-specific behavior. `HoneyDrunk.Memory.Abstractions` references only `HoneyDrunk.Kernel.Abstractions`; the runtime references only `*.Abstractions` packages; providers reference only `HoneyDrunk.Memory.Abstractions`. Memory content, entry tags and query tags never go into telemetry or logs. Verify shared capabilities in current code before reusing them; a catalog entry or scaffold is not an implemented integration.

Work within the selected request. Preserve unrelated changes and use a separate worktree when needed. Review the final diff, use Conventional Commits and ready-for-review PRs with exactly one accurate `Authorship:` line and a `Request:` line; include the authorship in commit trailers. Run meaningful checks for the affected behavior and report the reviewed/tested revision, failures and unrun checks. For documentation-only changes, check links, paths and instruction consistency. Preserve required checks and inspect actual latest-head Sonar new-code findings where analysis applies; do not suppress findings or weaken gates to obtain a pass. Legacy Grid Review is retired; do not restore its workers, queues or bypass labels. A configured replacement reviewer is not evidence of a completed review or enforcing merge check.

## Code Review Rules

Apply the [shared review criteria](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards/blob/main/HoneyDrunk.Standards/docs/CONVENTIONS.md#code-review) to changed behavior, using the repository boundaries above. Report actionable findings with the failing path, concrete impact and a small corrective action; disclose unavailable evidence. These rules grant no cross-repository access or merge authority.

- Review against the selected responsibility and actual tracked files. A repository name, catalog entry or open PR is not an implemented contract; do not invent ownership, public APIs or a runtime framework beyond what is checked in.
- Check real existing shared capabilities and dependency direction before accepting duplicate mechanics. Trace trust/tenant boundaries, scope enforcement, failure/retry/cancellation and bounded work for the behavior actually added; keep product policy in its selected owner.
- Require verification that exists and fits the change: observable behavior/contract tests, the contract-shape canary, and relevant security/concurrency tests for new code. Do not fabricate build commands, benchmark results, analysis coverage or deployment readiness.
