# Building Match & Flame

## What is not in the repository

| Item | Why | What to do |
|---|---|---|
| Google Mobile Ads for Unity 11.3.0 | third-party package, large | Import `GoogleMobileAds-v11.3.0.unitypackage` (Assets → Import Package → Custom Package). Then Assets → Google Mobile Ads → Settings → your Android App ID |
| `Assets/Scripts/AdIds.cs` | your ad unit IDs | Copy `BuildSetup/AdIds.Example.cs` → `Assets/Scripts/AdIds.cs`. It builds with Google test IDs; put your own IDs for release |
| Upload keystore | signing key | Create your own (Player → Publishing Settings → Keystore Manager). Never commit it |
| Privacy policy | required by Google Play | Put your URL into `Assets/Scripts/AppLinks.cs` (`PrivacyPolicyUrl`) |

## Steps

1. Install **Unity 6000.2.13f1** with Android Build Support (OpenJDK, SDK/NDK). WebGL support is needed only for the Yandex Games build.
2. Open the project, switch to Android (File → Build Profiles).
3. Import Google Mobile Ads and create `AdIds.cs` (see above).
4. Assets → External Dependency Manager → Android Resolver → Force Resolve.
5. Player Settings: set your **package name**, version, keystore (Publishing Settings → Custom Keystore) and its passwords — Unity does not store passwords.
6. Build: **Tools → Match & Flame → Build → Release AAB for Google Play**.
   - Before building it checks the privacy URL, real ad IDs, versionCode, package name, keystore, targetSdk ≥ 35 and minSdk ≥ 24.
   - Output: `../Builds/MatchFlame-<version>-v<code>.aab` next to the project folder; IL2CPP symbols are included in the bundle.
   - Without the editor: `Unity.exe -batchmode -quit -projectPath <project> -buildTarget Android -executeMethod ReleaseBuild.Aab -logFile <log>` with passwords in the `MF_KEYSTORE_PASS` / `MF_KEY_PASS` environment variables.
7. Raise **Bundle Version Code** for every upload and update `ReleaseBuild.PublishedVersionCode`.

## Editor tools

- **Tools → Match & Flame → Level Lab** — level builder, solver and simulator.
- **Tools → Match & Flame → UI Lab** — UI layout audit on 6 resolutions (overlaps, safe area, 48 dp touch targets, 14 sp fonts, contrast).
- **Tools → Match & Flame → Retention** — Hearth art import, daily level generator, tests and simulation.
- `Tools/retention_art/*.py` — procedural Hearth art (Python 3 + Pillow + NumPy): `python Tools/retention_art/draw_atlas.py .`, then `draw_room.py`, `draw_ui.py`, then Tools → Match & Flame → Retention → Import hearth art.
