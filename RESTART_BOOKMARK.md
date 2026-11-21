# RESTART BOOKMARK: Project LIpstick
**Generated At:** 2026-11-21T16:27:30+05:30  
**Machine State:** Preparing for PC Reboot  
**Git Branch:** `main` (cleanly committed and pushed)  
**Latest Milestone Landed:** Milestone 1 (Shell, Navigation & Clean Compilation - commit `9d0fb89f`)  
**Active Milestone in Flight:** Milestone 2 (Authentication, LinkedIn Session & AI Provider Hub)

---

## 1. Executive Summary & Status
All initial groundwork, core architecture, and foundation milestones have successfully landed:
- **M0 (Feasibility & Decisions - Issue #11):** Completed & closed. Architecture decisions recorded in `architecture/decisions.md`. Rubric reconciled to authoritative 12 categories totaling exactly 100 points (`data/rubric.json`).
- **Milestone 1 (Shell & Clean Compilation - Issue #1 & #9):** Passed milestone gate. Zero compilation errors across solution `LIpstick.slnx`. WinUI 3 Navigation shell, Mica backdrop, custom TitleBar, and XAML bindings fully resolved.
- **Milestone 2 (Authentication & AI Hub - Issues #2, #3):** Core domain models, DPAPI secure storage (`DpapiSecureStorageService`), AI providers (Gemini & OpenAI), and LinkedIn session worker implemented. Custom AI base URLs added to Settings.
- **Milestones 3–6 (Scanner, Humanizer, Diff, Apply, Content Lab - Issues #4, #5, #6, #7, #8):**
  - C# Humanizer and 5-check heuristic detector (`BURSTINESS`, `SPECIFICITY`, `SLOP DENSITY`, `FINGERPRINT`, `VOICE`) ported and verified 1:1 against Python reference.
  - 100-point Rubric Evaluator with deterministic "Fix first" ordering.
  - Word-level Diff Engine with additions and deletions tracking.
  - Content Lab service with all 11 LinkedIn skills cataloged, link-stripping algorithms, and character counters.
- **Feature 25 (Session Restart Bookmark):** Added and tested (`ST-03a` through `ST-03d`). Saves and restores in-flight drafts, active section, model ID, and session state across app crashes or machine reboots.
- **Automated Test Coverage:** Expanded to **114 automated unit, contract, and integration tests** in `tests/LIpstick.Tests`.

---

## 2. GitHub Issue Status & Milestones

| Issue # | Title | Status | Notes |
|:---|:---|:---:|:---|
| **#11** | M0: Verify platform and provider capabilities | **CLOSED** | Decisions documented in `architecture/decisions.md` |
| **#1** | Phase 1: Build .NET 10 WinUI 3 Fluent application shell | **COMPLETE** | Solution compiles with 0 errors/warnings; commit `9d0fb89f` pushed |
| **#9** | Cross-cutting: Configuration, theme, privacy, storage & bookmarks | **COMPLETE** | DPAPI storage, atomic writes, corrupt recovery (ST-01), clear data (ST-02), session bookmark (ST-03) |
| **#2** | Phase 2: LinkedIn OAuth, WebView2 session, credential protection | **IN PROGRESS** | Session info DTOs, mock client, sync worker landed; WebView2 live handshake wired |
| **#3** | Phase 2: Gemini & OpenAI provider adapters & model selector | **IN PROGRESS** | Provider adapters, error mapping, custom endpoint settings in SettingsPage landed |
| **#4** | Phase 3: Profile ingest and 100-point rubric scanner | **READY FOR GATE** | 12 categories (12/10/10/8/6/14/10/8/8/6/4/4), radial gauge, fix-first list |
| **#5** | Phase 4: Port li-human cleaner and 5-check detector to C# | **VERIFIED** | CleanText + 5-check detector pass all test thresholds (overall >= 70, min >= 55) |
| **#6** | Phase 4: Recommendations split view, diffing, live scorecard | **VERIFIED** | Side-by-side As-Is vs To-Be with live humanizer pill and metric progress bars |
| **#7** | Phase 5: Safe LinkedIn apply, backup history, and rollback | **VERIFIED** | Threshold gate prevents apply on non-PASS; atomic backup to `history.json` |
| **#8** | Phase 6: Content Lab for the 11 LinkedIn skills | **VERIFIED** | Skills 1–11 defined, link-suppression gate, hashtag count check |
| **#10**| Release hardening: integration, accessibility, end-to-end | **EXPANDING** | 114 tests active |

---

## 3. Local State & Artifact Locations
Persistent app configuration and rollback artifacts are located under `%LocalAppData%\LIpstick\`:
- `config.json`: Selected theme (Light/Dark/System), active model (`gemini-2.0-flash`), custom endpoints.
- `history.json`: Rollback snapshot history for profile updates.
- `secure_vault.dat`: Windows DPAPI-encrypted API keys and session tokens.
- `voice.md`: Personal voice and positioning template.
- `plan.md`: 4x/week publishing strategy and engagement schedule.
- `bookmark.json`: Active session bookmark storing in-flight draft, active page route, and model selection.

---

## 4. How to Resume After Machine Reboot

1. **Open Terminal in Repository:**
   ```powershell
   cd D:\Git-Projects\LIpstick
   ```

2. **Verify Solution Build:**
   ```powershell
   dotnet build LIpstick.slnx
   ```

3. **Run Full Test Suite:**
   ```powershell
   dotnet test tests/LIpstick.Tests/LIpstick.Tests.csproj
   ```

4. **Launch Application:**
   ```powershell
   dotnet run --project src/LIpstick.App/LIpstick.App.csproj
   ```
   *The application will read `%LocalAppData%\LIpstick\bookmark.json` and automatically resume where you left off.*

5. **Continue Teamwork or Single Agent:**
   - Prompt: `"Resume Project LIpstick from RESTART_BOOKMARK.md. Milestone 1 is landed and M2 is active."`

---

## 5. Resume Notes (2026-11-21)
- On resume, the worktree contained uncommitted Milestone 2 changes; preserve and continue these edits.
- Verification at resume: `dotnet build LIpstick.slnx --no-restore` succeeded (0 errors); test suite passed (120/120).
- Follow-up fixes in this session: registered both Gemini and OpenAI with `IAIHubService`; added persistent custom base URLs to `AppConfig`, applied them at startup, and validated HTTP(S) endpoint input.
- Build after these fixes also succeeded (0 errors; existing analyzer warnings).
- Initial resume finding, superseded by the continuation below: app was using the mock LinkedIn client and the real OAuth flow was not wired.

### Latest continuation update
- Replaced app mock LinkedIn registration with `LinkedInOAuthClient`; added system-browser authorization, loopback callback/state validation, DPAPI-backed Client ID storage, OIDC userinfo identity storage, and explicit profile-fetch failures instead of demo fallback.
- Aligned README, SPEC, and architecture decision record with native system-browser OAuth using PKCE (S256); no embedded client secret. LinkedIn must enable native PKCE for the app.
- Build succeeded (0 errors); test suite passed 125/125 after PKCE and AI adapter contract updates, including checks that missing credentials do not fabricate sessions and failed profile requests do not return demo data. No real LinkedIn OAuth was possible without developer app credentials.
- **Next:** Complete the Phase 2 gate in `TEST-PLAN.md`: verify live model availability and key-validation UX, complete LinkedIn LI-01–LI-06 behavior, and record the Phase 2 acceptance evidence. Provider routing, SDK cancellation, typed errors, usage/latency, and custom endpoints are now covered. Real LinkedIn sign-in still requires LinkedIn to enable native PKCE for the developer app. Issues #2/#3 remain in progress.


### SDK integration update
- Migrated Gemini and OpenAI adapters to the official .NET SDKs (Google.GenAI 2.2.0 and OpenAI 2.15.0). Preserved injectable HTTP transports, custom base URLs, cancellation, typed failures, usage counts, and latency metadata; OpenAI SDK telemetry is disabled.
- Added tests for SDK cancellation and both custom endpoint routes. Latest verification: build succeeded (0 warnings, 0 errors); tests passed 127/127.

### Continuation update (2026-11-21)
- Implemented native LinkedIn refresh-token exchange in `LinkedInOAuthClient`: rotated access/refresh tokens and expiry are saved to DPAPI storage; failed refresh clears the stale session; cancellation leaves credentials intact. Added coverage for refresh success, failure cleanup, and cancellation.
- AI provider credential Save & Connect now sends a small fixed validation request. It reports verified status only after a successful provider response and restores the previous key on failure; keys remain in secure storage and validation requests include no profile data.
- Removed the fabricated LinkedIn starter identity/headline from Connections. The connected state now shows OIDC userinfo name/avatar and transparently reports when headline is unavailable with current permissions.
- Latest verification: build succeeded (224 warnings, 0 errors); tests passed 130/130. Includes native OAuth token refresh success/failure/cancellation, userinfo identity persistence, AI credential validation compilation, provider routing/cancellation/custom endpoints.
- **Next:** Address remaining LI-01–LI-06 acceptance gaps: real profile-ingest limits, safe secret/log evidence, and live native PKCE/refresh behavior depend on LinkedIn app permissions and credentials. Record only externally verifiable acceptance results; no live credentials are present.

### UI wireframe alignment update (2026-11-21)
- Fixed the app startup crash: DI could not choose between the two `AIHubService` constructors. `IAIHubService` is now explicitly constructed from registered `IAIProvider` instances.
- Compared the running app with `Connections-Content-History-Settings.png`, `Scanner.png`, and `Improve.png`. Set a centered 1280x850 initial window; moved persistent account states to the top of navigation; aligned Connections, Scanner, Improve, Content Lab, History, and Settings to the pale blue canvas, navy hierarchy, rounded white cards, section tabs, score rows, and filter/table patterns from the references.
- Removed fabricated default profile/post content from Scanner, Improve, and Content Lab. Disconnected Scanner/Improve now show connection guidance. Content Lab tabs use compact wireframe labels. History section/date/status filters are functional.
- Fixed startup DI ambiguity between `AIHubService` constructors. UIA navigation smoke review reached Scanner, Improve, Content Lab, History, and Settings without a crash; captured UI previews directly from LIpstick's window (not the overlapping Amazon/browser window).
- Latest solution build succeeded (229 warnings, 0 errors); `git diff --check` passed (line-ending notices only). Tests were not rerun for this visual pass.
- **Next:** Keep the app open for visual review. Continue keyboard/accessibility and responsive-size refinement if needed; current Content Lab only generates the post workflow even though its skill selector displays all 11 tools. External LinkedIn/AI credentials are still required for connected states and generated provider content.

### Finalized handoff — 2026-11-21
- Content Lab now dispatches all 11 skills and renders per-skill input/output surfaces. The deterministic service workflows are still under review for skill-contract fidelity and fabricated-data safeguards; Phase 6 remains In progress (#8).
- GitHub Project LIpstick: #2 LinkedIn OAuth, #3 provider adapters, and #8 Content Lab are In progress. UI fidelity bugs #12–#14 are Backlog.
- Visual worktree changes are ready for user review; the app was not relaunched after the last modifications.
- LinkedIn public-page scans do not require a developer app. Live AI provider validation still requires provider credentials; wireframe visual comparison remains tracked by #12–#14.
- Next after restart: inspect this branch/worktree and commit history; resume Phase 6 contract/factuality review, then capture the remaining Phase 2 live-credential evidence. Do not mark these milestones complete without evidence.

### Superseded LinkedIn product-flow note
- The earlier snapshot-only/no-fetch decision was replaced by the user instruction to fetch one public profile URL during each explicit scan. The latest behavior and limits are documented below.

### LinkedIn public-page scan decision — 2026-11-21 (supersedes prior snapshot-only implementation note)
- User chose one-profile public-page fetching. Each explicit Scan makes one unauthenticated HTTP GET to the saved LinkedIn member URL; no browser cookies, passwords, bulk collection, CAPTCHA bypass, or sign-in-wall bypass.
- Parser extracts what public HTML/metadata exposes; LinkedIn may return partial content or block direct requests. In that case scan the cached user snapshot or prompt for pasted profile text.
- Login button still opens default browser. The browser remembers its own login; LIpstick does not use it for requests.
- Code updated in `BrowserLinkedInProfileClient` for HTTP fetch, same-domain HTTPS redirect handling, no cookie container, public HTML parsing, cache/fallback, and scan-source status. Added HTML parser fixture coverage. Docs/issue #4 updated to the public-scan decision.
- Verification: solution build passed (164 warnings, 0 errors); focused LinkedInProfileSnapshotTests passed 4/4; git diff --check passed. The provided public profile URL returned HTTP 200 to a single cookie-free request. Next: review and push this implementation checkpoint.
