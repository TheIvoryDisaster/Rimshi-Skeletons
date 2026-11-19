# Workshop publishing: local release watcher

GitHub-hosted SteamCMD authentication has been retired. Rimshi releases are watched and uploaded by GutenStructure's **local Windows Workshop Watcher**, which keeps Steam authentication on your PC.

Follow [the Workshop Watcher setup and commands](https://github.com/TheIvoryDisaster/GutenStructure/blob/beta/docs/workshop-watcher.md). The watcher implementation/fixes are in GutenStructure #42/#43 on beta; use the reviewed branch/pin you installed until promoted. Keep GitHub release builds and their ZIP assets enabled: the watcher consumes those artifacts.

Existing target: RimWorld app **294100**, Workshop item **3841311958**. The owner verified the v20 local upload and subscriber gameplay. Keep that item ID so updates do not create a new listing. Check visibility and subscriber installation after updates. Local account authorization and item ownership remain required.

The removed `workshop-upload.yml` and `workshop-preview.yml` workflows, Python staging/upload adapters and adapter tests served the superseded hosted experiment. No hosted Steam workflow remains in this change. It does not remove the watcher or change mod/build/release behavior.

## Remove obsolete GitHub credentials

In this repository's Settings → Environments → `steam-publish`, delete `STEAM_USERNAME`, `STEAM_PASSWORD` and `STEAM_GUARD_CODE` if present. Delete the environment if unused. Check repository-level Actions secrets too if you stored copies there. No replacements are needed for the watcher: keep its local Steam session and its encrypted read-only GitHub token for private releases. Never upload either to GitHub secrets, caches, artifacts or chat. Deleting a secret does not revoke Steam sessions; revoke unwanted hosted-runner authorizations in Steam separately.

Nexus publishing is separate and uses an author-owned `nexus-publish` environment/API key. See [GutenStructure's Nexus guide](https://github.com/TheIvoryDisaster/GutenStructure/blob/codex/nexus-only-publisher/docs/nexus-publishing.md). Its current pilot handles public Meitou/Kenshi release ZIPs, not this private RimWorld repository.
