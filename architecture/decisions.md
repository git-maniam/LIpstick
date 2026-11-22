# M0 Architecture & Feasibility Decision Record

**Project:** LIpstick (Windows Modern UI for LinkedIn)  
**Date:** 2026-11-21  
**Status:** Approved  
**Related Issue:** #11 (Blocks #1 - #10)

---

## 1. Context & Background

LIpstick bridges personal LinkedIn optimization with multi-provider generative AI (Google Gemini & OpenAI) in a native Windows Modern Fluent desktop experience. Before embarking on application construction, M0 resolves platform capabilities, SDK selections, security boundaries, and API feasibility.

---

## 2. Key Decisions

### 2.1 Desktop Framework & Target Platform
- **Decision:** WinUI 3 with Windows App SDK 1.6+ on .NET 10 (C# 14). The WPF alternative mentioned in early drafts is officially rejected.
- **Packaging Strategy:** Dual-mode packaging:
  - **Development & Automated Testing:** Unpackaged WinUI 3 (`<WindowsPackageType>None</WindowsPackageType>`). Enables seamless CLI compilation (`dotnet build`, `dotnet test`, `dotnet run`) without code-signing certificate overhead or developer-mode sideloading constraints.
  - **Release:** Packaged MSIX with sparse package / app identity for production deployment.
- **Target OS:** Windows 11 (build 22000+) primary, Windows 10 (build 19041+) supported.
- **Target Architecture:** `win-x64` (primary), `win-arm64` supported.
- **UI & Materials:** Fluent Design System, Mica backdrop with fallback to solid background on downlevel builds, Acrylic flyouts, 8–12px corner radii, Segoe UI Variable typography.

### 2.2 LinkedIn Profile Setup, PDF Import & Public Scan
- **Login shortcut:** Open LinkedIn login in the system default browser. This does not authenticate LIpstick or grant access to browser data. LIpstick never requests, stores, reads, or reuses LinkedIn passwords, cookies, or browser tokens.
- **Setup:** Connections persists a public member URL and offers an adjacent **CHECK** action that makes one unauthenticated request and reports whether the page is readable. It also explains LinkedIn desktop's **Save to PDF** steps and imports the user's selected PDF locally.
- **Initial scan:** Require a checked URL and imported PDF. Fetch the public page, combine the returned fields with PDF text, preserve provenance, and save the profile/score baseline.
- **Re-scan:** After the user manually updates their LinkedIn profile, fetch the URL again and evaluate only fresh public-page content. Keep the original baseline for comparison; never reuse the initial PDF as current content.
- **Limits:** Do not bypass sign-in walls, CAPTCHAs, rate limits, or other access controls. Public pages may omit sections; report inaccessible or missing fields clearly and do not invent data.
- **Updates:** Recommendations are manual. LIpstick never writes profile changes or reports an unperformed write as successful. Local history records local snapshots/recommendations only.

### 2.3 AI Provider Architecture & Model Hub
- **Authentication:** Direct API Key management backed by Windows Credential Locker / DPAPI.
- **Google Gemini:** `Google.GenAI` SDK / REST client with `gemini-flash-latest` as the connection-validation/default model; also offer the configured Gemini Flash models when the account provides access.
- **OpenAI:** `OpenAI` official .NET SDK with support for `gpt-4o`, `gpt-4o-mini`, `o3-mini`, `o1`.
- **Resilience & Testing:** Deterministic `MockAIProvider` for all unit and integration test executions without leaking secrets or consuming live quotas.
- **Safety & Untrusted Content:** Imported LinkedIn text and LLM responses are treated as untrusted; structured validation prevents prompt injection or automatic execution.

### 2.4 Credential Storage & Privacy Boundaries
- **Credentials:** API keys, access tokens, and session secrets are encrypted via Windows Data Protection API (DPAPI) and Windows Credential Locker (`PasswordVault`).
- **Forbidden:** No tokens, API keys, or private profile identifiers in plain-text configuration files, telemetry, diagnostics, or source code.
- **Local State Location:** `%LocalAppData%\LIpstick\`:
  - `config.json` (preferences, selected model, theme — secrets excluded)
  - `history.json` (atomic rollback snapshots)
  - `voice.md` (personal tone configuration)
  - `plan.md` (weekly content calendar)
- **Data Deletion:** Clear Local Data removes all local artifacts upon confirmed user prompt (ST-02).

### 2.5 Rubric Reconciliation
- **Authoritative Source:** `SPEC.md` and `TEST-PLAN.md` define the authoritative 12 categories totaling exactly 100 points:
  1. Headline: 12 pts
  2. About: First 2 Lines: 10 pts
  3. About: Body & Proof: 10 pts
  4. Featured Items: 8 pts
  5. Banner & Visual Positioning: 6 pts
  6. Experience Outcomes & Metrics: 14 pts
  7. Recommendations: 10 pts
  8. Skills & Badges: 8 pts
  9. Creator Mode Cadence: 8 pts
  10. Education & Credentials: 6 pts
  11. Custom URL & Contact Clarity: 4 pts
  12. Headshot & Aesthetic Consistency: 4 pts
  Total = 100 points (Max check: 12/10/10/8/6/14/10/8/8/6/4/4).
- `data/rubric.json` is updated to reconcile the headshot and category breakdown with SPEC.md.

### 2.6 Humanizer Engine Acceptance Thresholds
- **Pass Threshold:** Overall human score $\ge 70$ AND every individual check (Burstiness, Specificity, Slop Density, Fingerprint, Voice) $\ge 55$.
- **Review:** Overall score between 50 and 69.
- **Flagged:** Overall score $< 50$ or any metric $< 55$.
- The copy-suggestion action is available only when status is `PASS`; the user remains responsible for applying the copied text in LinkedIn.
