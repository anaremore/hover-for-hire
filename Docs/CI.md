# Continuous integration

`.github/workflows/ci.yml` runs on every push to `main`, on pull requests, and on demand from the Actions tab. It uses [GameCI](https://game.ci) and runs two jobs:
* **Tests:** EditMode and PlayMode, with results uploaded as artifacts.
* **Builds:** release builds for Windows, macOS and Linux. Each goes through the project's own build method (`ProjectSetup.BuildWindows` / `BuildMac` / `BuildLinux`) and is uploaded as an artifact for 14 days. All three platforms build on Linux runners (pinned to Ubuntu 24.04), since the project uses the Mono scripting backend.

## What the repository owner needs to add

Unity needs a license to run in CI, and the credentials are yours to add. Until they are set, the workflow's first job posts a notice and the test and build jobs are skipped, rather than failing every push.

Add the secrets under **GitHub → Settings → Secrets and variables → Actions**, following [GameCI's activation guide](https://game.ci/docs/github/activation). They depend on your license:

| License | Secrets |
| --- | --- |
| Personal | `UNITY_LICENSE` (the contents of your `.ulf` file), `UNITY_EMAIL`, `UNITY_PASSWORD` |
| Professional | `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD` |

GameCI uses them only to activate Unity during the job.

## Notes

- **Git LFS.** The checkout fetches LFS objects: the helicopter model and the terrain textures, a few megabytes per job. This counts against the repository's LFS bandwidth.
- **Library cache.** The imported `Library` folder is cached per job, keyed on the package lock and the Unity version, and refreshed each commit.
- **Releases.** CI does not publish them. Releases are made from local builds with `Tools/package_builds.py`, which refuses development builds and builds of another commit.
