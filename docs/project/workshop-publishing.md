# Workshop update preview and credential primer

Existing item: https://steamcommunity.com/sharedfiles/filedetails/?id=3841311958

This is an **update** target, never a new-item request. The owner reports the page is unlisted. No workflow in this change logs into Steam, reads Steam credentials, changes visibility or uploads content. Remote ownership/visibility are not verified by this dry run.

## Run the preview

The `Preview Steam Workshop update (no login)` workflow calls a pinned GutenStructure helper. It downloads an existing release from this private repository using this repository's read-only GITHUB_TOKEN. It does not rebuild the mod, need a PAT, or upload private assets into GutenStructure.

The initial PR runs automatically against release v19. Manual dispatch becomes available in Actions once the caller workflow is on the default branch. Its defaults are v19, asset 758368415 and SHA-256 `0837b425606bd19020dc113da4c2ea9c852a765222091e3e3c194585a1396f98`. They are an explicit tested snapshot, not a moving latest release; update all three together for later tests.

A passing run produces `rimworld-workshop-preview`, retained for one day in this private repository. Download it from the Actions run to inspect:

- `plan.json`: existing item ID, app 294100, package ID, source commit, digest and supported versions; `uploaded` remains false.
- `workshop-preview.vdf`: proposed update target and runner staging path. No visibility/title/description overrides are requested.
- The validated staged mod folder. The v19 release has a 1.6 DLL and advertises 1.6 only; no 1.7 assembly is fabricated.

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
