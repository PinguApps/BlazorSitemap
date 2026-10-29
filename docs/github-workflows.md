# GitHub workflow reconciliation

The automation follows `PinguApps/Aspire.Hosting.PostgreSQL.Railway`'s six-workflow structure. All jobs with a runner use `blacksmith-2vcpu-ubuntu-2404`; reusable jobs inherit that runner. Action commit pins match the reference repository.

| File | Behavior | Adaptation for BlazorSitemap |
| --- | --- | --- |
| `_run-tests.yml` | Reusable/manual test run, configurable check/comment reporting, TRX summary and artifact upload | Uses `eng/Verify.ps1`: pack both NuGet artifacts first, restore consumers from an isolated local feed, run the full Gherkin suite. Keeps checkout-ref and reporting inputs. Removes Aspire pin checks/CLI, Railway credentials and live-provider filters. No Windows runner matrix. |
| `pr-validation.yml` | PR opened/synchronized/reopened/ready-for-review and manual validation; cancels superseded runs | Calls the reusable suite with the reference reporting/check settings. Fork PRs do not publish GitHub check/comments. Removes the TypeScript package gate and credential-gated live Railway job: packed Blazor consumers are already part of verification. No publishing secrets or NuGet login. |
| `pr-labels.yml` | Metadata-only auto-labelling on PR opened/edited | Same pinned labeller and checkbox pattern as the reference. Uses `pull_request_target` without checking out or executing PR code. |
| `release-drafter.yml` | Draft refresh on main pushes or manual dispatch | Explicit full SemVer template; validates the generated name and tag with `eng/Get-ReleaseVersion.ps1`. |
| `publish-release-draft.yml` | Manual or scheduled publication of an existing draft when main has commits since the last release; then invokes package publishing | Same draft/tag/ancestry guards and reusable publishing call. Schedule is **Sunday 09:39 UTC**, changed from 09:32. It does not create the first release automatically and refuses multiple drafts. |
| `publish.yml` | Published GitHub releases, explicit tag dispatch or reusable tag invocation; OIDC NuGet login; package and symbol publishing | Checks out an explicit tag and validates it with `eng/Get-ReleaseVersion.ps1` before .NET setup, packing or NuGet login. Uses `eng/Verify.ps1 -PackageVersion` and pushes both packages/symbols. |

The release-drafter configuration and PR categorisation template also match the reference repository. Labels include release-size choices and feature, bug, docs, security and meta categories.

Release names and tags must match exactly, for example `v2.0.1`. The shared validator requires all three version components, rejects leading zeroes and malformed prerelease/build identifiers, and returns the package version without `v`. Explicit package publishing supports SemVer prereleases and build metadata (NuGet normalizes build metadata out of package identity). Scheduled draft publication retains its stable-only policy and validates both name and tag **before** making the release public. Abbreviated names such as `v2` fail even when the tag is complete. Gherkin release-version scenarios run the same validator as the workflows.

GitHub's manual release editor can bypass repository workflows when making a release public. Package publication rejects an invalid release event; maintainers must use a complete matching name and tag in that editor too.

PR tests use ordinary public NuGet restoration and need no NuGet API key, provider credentials or publishing login. Only publishing uses NuGet trusted publishing (`NUGET_USER` plus the action's temporary OIDC API key). The GitHub repository must have Blacksmith enabled, relevant labels and NuGet trusted-publishing configuration for **both** package IDs.

The draft schedule deliberately publishes a release when its guards pass. GitHub's default token does not trigger another workflow from that release event, so the draft workflow explicitly calls the reusable publish workflow, as in the reference. Workflow configuration does not publish anything during local implementation or verification.

Validation runs `actionlint` with the custom Blacksmith label allowed and ShellCheck on the shell steps. Local product validation runs the same `eng/Verify.ps1` entry point used in CI. GitHub permissions, repository services and OIDC credentials can only be exercised by actual hosted workflow runs; local checks do not prove those external settings exist.
