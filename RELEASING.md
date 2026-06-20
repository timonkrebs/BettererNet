# Releasing

BettererNet publishes all packable projects to [nuget.org](https://www.nuget.org/) via the
[`release`](.github/workflows/release.yml) workflow.

## One-time setup

Publishing uses [nuget.org Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing)
(OIDC) — there is **no API key secret to manage**. Two one-time steps:

1. **Create a protected environment.** In repo Settings → Environments, add one named
   **`nuget-release`**. Limit its deployment branches/tags to the `v*` tag pattern (and optionally
   add yourself as a required reviewer). Real publishes run in this environment; dry runs don't, so
   packaging can still be verified on any branch.
2. **Add the Trusted Publishing policy** on nuget.org (Account → Trusted Publishing):
   - **Package owner:** `tsharp`
   - **Repository owner:** `timonkrebs`, **Repository:** `BettererNet`
   - **Workflow:** `release.yml`
   - **Environment:** `nuget-release`

   The environment binding matters for least privilege: a nuget.org policy applies to **all** packages
   owned by the owner, so requiring `nuget-release` ensures only approved, `v*`-tag-gated runs can
   mint a publish key.

The workflow requests the OIDC token (`id-token: write`) and the
[`NuGet/login`](https://github.com/NuGet/login) action exchanges it for a short-lived key, so nothing
else is needed.

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

You can also run it manually from the **Actions → Release** tab and supply the version. Manual runs
**default to a dry run** (build + test + pack, no push) so you can verify packaging on any branch;
uncheck **“Pack only — do not push to NuGet”** to actually publish. Tag pushes always publish.

## Compatibility

BettererNet is pre-1.0; public APIs may still change between releases. Note that the CLI's
`ConfigLoader` unifies the bundled `BettererNet.*` assemblies with a user's compiled config, so once
a version is published, changing a public test-factory signature can break already-compiled configs
at runtime. After 1.0, prefer additive overloads (or append optional parameters) over editing an
existing signature.

## Verifying

After the workflow succeeds, confirm the global tool installs end-to-end:

```bash
dotnet tool install -g BettererNet.Cli --version 0.1.0-alpha
betterernet init && betterernet ci
```
