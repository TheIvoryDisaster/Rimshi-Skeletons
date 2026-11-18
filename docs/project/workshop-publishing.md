# Workshop update preview and credential primer

Existing item: https://steamcommunity.com/sharedfiles/filedetails/?id=3841311958

This is an **update** target, never a new-item request. The owner reports the page is unlisted. No workflow in this change logs into Steam, reads Steam credentials, changes visibility or uploads content. Remote ownership/visibility are not verified by this dry run.

## Run the preview

The `Preview Steam Workshop update (no login)` workflow calls a pinned GutenStructure helper. It downloads an existing release from this private repository using this repository's read-only GITHUB_TOKEN. It does not rebuild the mod, need a PAT, or upload private assets into GutenStructure.

The initial PR runs automatically against release v19. Manual dispatch becomes available in Actions once the caller workflow is on the default branch. Its defaults are v19, asset 758368415 and SHA-256 `0837b425606bd19020dc113da4c2ea9c852a765222091e3e3c194585a1396f98`. They are an explicit tested snapshot, not a moving latest release; update all three together for later tests.

A passing run writes its validation report and VDF directly in the private Actions run summary. This avoids the account's full artifact-storage quota. The optional central `save-preview-artifact` setting defaults to false; enable it only when quota is available, to retain the staged folder for one day. Inspect:

- `plan.json`: existing item ID, app 294100, package ID, source commit, digest and supported versions; `uploaded` remains false.
- `workshop-preview.vdf`: proposed update target and runner staging path. No visibility/title/description overrides are requested.
- The validated staged mod folder is ephemeral unless optional artifact saving is enabled. The v19 release has a 1.6 DLL and advertises 1.6 only; no 1.7 assembly is fabricated.

The VDF's absolute runner path will not exist on your computer. Regenerate it with the central helper against a local output path before any future separately approved upload. Do not treat this artifact as an executable upload instruction. Check the mod still matches your tested in-game version; staging checks do not prove gameplay behavior.

## Steam credentials: do not add them for this preview

The current workflow accepts no Steam credentials. A live upload adapter is not implemented or tested. No password is needed to get a useful preparation result.

For a future reviewed, permitted adapter, the intended storage location is **this repository → Settings → Environments → New environment → steam-publish**, not GutenStructure:

1. Require your approval, restrict publishing to the approved branch and disable administrator bypass where supported. Verify your private-repository plan supports the needed protection rules. If you are both initiator and reviewer, prevent-self-review must be off, with an explicit owner-only uploader dispatch check. The preview creates none of these controls.
2. Your Steam login account name would be environment variable `STEAM_USERNAME` (not your display name or Workshop ID).
3. If password authentication is selected, your password would be environment secret `STEAM_PASSWORD`. These names are proposed and are **not read by this workflow**. Do not paste credentials into chat, source code, workflow inputs or shell command arguments.
4. Leave Steam Guard enabled. Fresh GitHub runners may need account-owner authorization; username/password alone may not work. Do not save a one-time code as a permanent secret or provide your authenticator seed/recovery codes. Any reusable session would need a reviewed encryption, lifetime and revocation design before being collected; it must never enter workflow artifacts or caches.

A Steam Web API key does not replace account authorization for a Workshop upload. Account sharing is not the plan: your personal credentials remain under your control. GitHub/runtime administrators and trusted publisher code can still potentially misuse runtime credentials, so review the complete pinned code before granting access.

Valve documents SteamCMD Workshop uploads for testing; that is not blanket confirmation of permitted unattended production automation. Before any live adapter, establish allowed use, prove RimWorld updates the existing item correctly, verify Steam Guard handling and confirm the unlisted visibility is preserved. Actual updates can continue through RimWorld's built-in uploader while these remain unresolved.

Central guide: https://github.com/TheIvoryDisaster/GutenStructure/blob/codex/mod-distribution-spike/docs/rimworld-workshop.md

This change targets beta for owner testing. It must not be promoted as an operational Steam uploader merely because the preview passes.

## v20 live adapter: dedicated Windows runner (staged for owner testing)

The owner verified a local SteamCMD update of item 3841311958 and subscriber gameplay with v20. The new `workshop-upload.yml` automates that path but has NOT yet been tested on a runner. It is manual-only, main-only and owner-only, uses the protected `steam-publish` environment, and accepts an exact release/asset/hash plus item confirmation. No release event uploads automatically yet. Merge to beta for review; promote to main only after owner feedback. Dispatch is intentionally disabled on beta and feature branches. Merging to main may trigger the repository's normal version-producing build; the uploader still uses the explicitly selected release.

### Credential setup (no Steam password in GitHub)

1. Use a dedicated Windows account/computer for publishing, not a shared development machine. Install SteamCMD outside the runner checkout, for example `C:\steam-publisher\steamcmd\steamcmd.exe`. Install Python 3.12+ and GitHub CLI (`gh`) on PATH. No global `gh auth login` is needed; the workflow supplies a temporary repository token.
2. Under the SAME Windows account that will run the runner, launch that SteamCMD and `login YOUR_LOGIN_NAME`; complete Steam Guard interactively, then `quit`. Test that a subsequent `login YOUR_LOGIN_NAME` can reuse authorization without a password. If it cannot, stop here: the workflow has no password fallback and must not disable Steam Guard. Local prior success does not prove reusable login on another account/machine.
3. Rimshi repository → Settings → Actions → Runners → New self-hosted runner → Windows/x64. Follow GitHub's generated commands. Add custom label `rimshi-steam-publisher`. Scope it to this private repository only. Initially run interactively as the same Windows account (`.\run.cmd`), not a service running as LocalSystem. Keep it online only when publishing. Never use its label for PR/build jobs; a self-hosted runner is not an isolation boundary against repository administrators or writable workflows. If untrusted collaborators gain workflow write access, use a separate owner-only publisher repository before continuing.
4. Repository → Settings → Environments → New environment → `steam-publish`. Restrict deployment branches to **main**. Where your GitHub plan supports it, require TheIvoryDisaster's approval, keep prevent-self-review OFF (you initiate and approve), and disable administrator bypass. Verify private-repository environment protection availability rather than assuming it is enforced. If unavailable, keep the runner offline until you have reviewed the main commit and intend to publish; manual dispatch alone is not equivalent to environment review.
5. In that environment's **Variables**, add `STEAM_USERNAME` (account login name, not display name) and `STEAMCMD_PATH` (e.g. `C:\steam-publisher\steamcmd\steamcmd.exe`). There is NO `STEAM_PASSWORD`, Guard code, authenticator seed, session ZIP or central GutenStructure secret for this adapter. Protect the Windows profile/SteamCMD session with disk encryption and account access controls; never commit/cache/upload it. Revoke sessions through Steam if compromised.
6. After this PR is reviewed on beta and promoted to main, Actions → Upload verified release to Steam Workshop → Run workflow → main. For v20 use asset `764119811`, SHA-256 `3899d98904f7e63ad2cea3e45e66949284998ea51d2070316030f87aee7e275c`, and confirmation `3841311958`. Approve the environment if configured. This intentionally uploads v20 again; select a newer tested release and its exact asset/hash when appropriate.
7. Expect a confirmed item-update summary. Raw Steam output is never published. A failed/expired login must be reauthorized locally; do not supply a code through workflow inputs. Verify visibility and subscriber installation after each pilot. Stop the runner afterward. Logs and staging are not uploaded; the Steam session remains local for subsequent authorization. There is no durable replay receipt yet, so dispatching again can repeat the same update.

The runner's local authenticated session is the credential. The assistant can inspect or dispatch authorized GitHub workflows; it does not need to know your password. Future fresh hosted-runner authentication and automatic release-event uploads need separate testing and permitted-use review. Local verified SteamCMD behavior is not a blanket platform terms assurance.
