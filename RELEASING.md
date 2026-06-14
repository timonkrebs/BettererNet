# Releasing

BettererNet publishes all packable projects to [nuget.org](https://www.nuget.org/) via the
[`release`](.github/workflows/release.yml) workflow.

## One-time setup

Add a repository secret named **`NUGET_API_KEY`** (Settings → Secrets and variables → Actions)
containing a nuget.org API key scoped to push the `BettererNet.*` packages.

## Cutting a release

1. Pick the version (e.g. `0.1.0-alpha`). Update `<Version>` in
   [`Directory.Build.props`](Directory.Build.props) if it differs.
2. Move the relevant notes under a new heading in [`CHANGELOG.md`](CHANGELOG.md) and update the
   compare links at the bottom.
3. Commit, then tag and push:

   ```bash
   git tag v0.1.0-alpha
   git push origin v0.1.0-alpha
   ```

   Pushing a `v*` tag runs the workflow, which builds, tests, packs with the tag's version, and
   pushes to nuget.org (`--skip-duplicate`, so re-runs are safe).

You can also trigger it manually from the **Actions → Release** tab and supply the version — useful
for a dry run on a branch.

## Verifying

After the workflow succeeds, confirm the global tool installs end-to-end:

```bash
dotnet tool install -g BettererNet.Cli --version 0.1.0-alpha
betterernet init && betterernet ci
```
