# LIpstick: Windows Modern UI for LinkedIn

> **LIpstick** (Lipstick for LinkedIn) — makeup and polish for your professional LinkedIn presence. Built with latest **.NET 10 (C# 14)** and **Windows Modern UI (WinUI 3 / Windows App SDK)**.

This repository contains the complete specification, data dictionaries, heuristic engines, architecture documents, and .NET project scaffolding required to build **LIpstick**. Approved UI references: [Scanner](Scanner.png), [Improve](Improve.png), and [Connections, Content Lab, History, Settings](Connections-Content-History-Settings.png).

---

## What is in this bundle?

```text
.
├── SPEC.md                      # Comprehensive Master Technical Specification (.NET 10 / WinUI 3)
├── README.md                    # Developer Guide & Project Overview
├── architecture/
│   ├── ui-wireframes.md         # Windows 11 Modern Fluent UI layouts & wireframes
│   └── api-contracts.md         # C# .NET 10 Provider interfaces for Gemini, OpenAI & LinkedIn
├── data/
│   ├── hooks.json               # 21 proven LinkedIn viral hook formulas
│   ├── rubric.json              # 100-point profile scoring rubric across 12 categories
│   └── slop.json                # Invisible Unicode, typographic & lexical AI slop dictionaries
├── templates/
│   ├── voice.md                 # User personal tone & positioning profile
│   └── plan-template.md         # 4x/week publishing strategy & engagement template
└── skills/                      # Complete specifications for all 11 core LinkedIn skills
    ├── li-post/                 # Feed post authoring
    ├── li-human/                # AI fingerprint cleaner & detector reference
    ├── li-comment/              # 9-archetype comment engine
    ├── li-reply/                # Thread triage & reply drafting
    ├── li-profile/              # 100-pt profile audit & rewrites
    ├── li-plan/                 # Weekly calendar & 10-person engagement
    ├── li-carousel/             # Slide-by-slide copy & PDF compiler
    ├── li-dm/                   # 200-character invite & 2-step follow-ups
    ├── li-inbox/                # Message triage & bot-sequence detection
    ├── li-audit/                # Empirical post-mortem analytics
    └── li-repurpose/            # Long-form asset extraction
```

---

## Key Features

1. **Windows Modern Fluent UI (.NET 10 & WinUI 3 / Windows App SDK):**
   - Native Windows 11 Fluent Design System with Mica backdrop material and rounded corners.
   - Light Theme by default with full Dark Theme support and system accent sync.
2. **Dual-AI Authentication & Multi-Provider Hub:**
   - Sign in with Google (Gemini 2.5 / 2.0 / 1.5 series via Google.GenAI SDK).
   - Sign in with OpenAI (GPT-4o, o3-mini via OpenAI .NET SDK).
   - Dynamic Model Selector dropdown in the modern custom title bar.
3. **LinkedIn Session & Profile Scan:**
   - Open LinkedIn login in the default browser; the browser remembers sign-in when the user chooses. LIpstick never receives or stores a LinkedIn password, cookie, or browser token.
   - Save a profile URL such as `https://www.linkedin.com/in/rsmaniam/`, then paste the visible profile text into Scanner. LIpstick saves and scans that local snapshot; paste an updated copy before rescanning.
   - LIpstick does not scrape LinkedIn pages or access the browser's signed-in session. LinkedIn's terms prohibit automated scraping, and full profile API access is not available to ordinary third-party apps.
4. **Side-by-Side Recommendations & Real-Time C# Humanizer:**
   - Side-by-side diff comparison between current profile and AI rewrite.
   - Real-time scoring and cleaning engine (invisible Unicode removal, typographic normalization, slop scrub) ensuring high human score ($\text{PASS} \ge 70$).
5. **Manual Profile Updates:**
   - Review and copy the verified humanized recommendation, then apply it yourself in LinkedIn.
   - Paste the updated profile text into Scanner to rescan. LIpstick never reports an unperformed LinkedIn update as successful.

---

## Development Prerequisites

- **.NET 10 SDK** (or higher)
- **Visual Studio 2026 / Visual Studio Code** with C# Dev Kit and Windows App SDK workloads
- Windows 11 (build 22000+) or Windows 10 (version 1809+, build 17763+)

### LinkedIn profile setup

Choose **Open LinkedIn Login** to sign in through your default browser, then save your LinkedIn profile URL in Connections. The browser manages saved credentials. Copy your visible profile text and paste it into Scanner before scanning; paste a fresh copy after profile edits. A LinkedIn developer app is not required. LinkedIn's terms prohibit automated profile scraping, so LIpstick does not read browser cookies or fetch profile pages itself.

## Dependency-first implementation plan

Track delivery in [Project LIpstick](https://github.com/users/git-maniam/projects/8). Architecture, software compatibility, security, and persistence are prerequisites for feature work.

| Milestone | Implementation and dependencies | Acceptance gate |
| --- | --- | --- |
| M0: Feasibility and decisions | Verify LinkedIn authentication, readable/writable fields, scopes, approvals, and refresh behavior against official documentation. Verify AI authentication/model availability. Select WinUI 3; establish Windows, CPU architecture, SDK/package, WebView2 distribution, packaging, signing, and update compatibility decisions. | Record evidence and pinned versions. Unsupported capabilities block dependent features; OAuth alone is not evidence of profile-edit access. |
| M1: Secure foundation | Issue #1 plus the storage/security foundations of #9 precede integrations. Separate UI, Core, Infrastructure, and tests; Core has no UI/SDK dependency. Add DI, configuration, protected credentials, redacted diagnostics, cancellation, and atomic persistence. | Shell, accessibility basics, secret isolation, and ST-01/ST-02 storage recovery/deletion cases pass. |
| M2: Connections and adapters | Issues #2/#3 depend on M0/M1. Add capability-aware clients, token lifecycle, model selection, typed errors, timeouts, cancellation, and rate-limit handling. | LI and AI cases pass; unavailable capabilities are visibly disabled. |
| M3: Profile and scoring | Issue #4 depends on M2. Normalize snapshots and field availability. Reconcile rubric data to SPEC.md before implementing evidence-backed scoring and scanner UI. | SC cases pass; inaccessible fields are distinct from empty fields. |
| M4: Humanizer and review | Issues #5/#6 depend on M2/M3. Implement deterministic cleaning/scoring, bounded rewrites, provenance, diffs, and manual-edit preservation. | HU/DF cases pass, including stale results and threshold boundaries. |
| M5: Apply and recovery | Issue #7 depends on M1-M4 and verified write permissions. Persist original snapshot and pending operation before mutation. Reconcile uncertain outcomes before retry; report partial success and confirm rollback. | AP cases pass; no blind retries or overwriting intervening edits. |
| M6: Content Lab | Issue #8 depends on shared provider, humanizer, and storage services. Split into eleven skill tasks; implement shared voice/draft/plan/log services first. | Expand CL cases per skill; drafting tools do not publish or send. |
| M7: Release | Issue #10 follows all required milestones. Complete packaging, supported-platform checks, accessibility, resilience, dependency review, and end-to-end acceptance. | All functional cases and release gates pass with recorded evidence. |

### Decisions to resolve before feature implementation

- Pin the .NET SDK and centralized package versions with locked restore. Record licenses, transitive dependencies, vulnerabilities, and update ownership.
- Finalize installation identity, signing, update behavior, WebView2 distribution, and backdrop fallbacks in M0.
- Store credentials with Windows protection; define logout/revocation, data retention, local file permissions, and deletion before connection work. Never put secrets in prompts, configuration, logs, or source.
- Explain which profile data is sent to the selected AI provider. Treat imported text and model output as untrusted; validate structure/length and prevent generated text from invoking writes.
- Extend contracts with capability flags, field availability, snapshot identity, typed provider errors, and update outcomes: succeeded, failed, unsupported, or outcome unknown.
- Reconcile known inconsistencies: rubric data assigns Headshot 6 points while SPEC.md assigns 4; Boolean update results cannot represent uncertain outcomes. SPEC.md is authoritative for rubric weights. Remove the WPF alternative when recording the WinUI 3 architecture decision.
- Centralize eligibility at overall score >=70 and each metric >=55. Label scoring as a heuristic, not proof of human authorship.
- Unsupported LinkedIn capabilities require a documented product decision before substituting a manual workflow.

### Milestone verification and execution

Use [TEST-PLAN.md](TEST-PLAN.md) and the milestone gates in [SPEC.md](SPEC.md). Include ST-01/ST-02 explicitly; add cases for capability denial, credential revocation, prompt injection, stale snapshots, and uncertain writes.

Batch necessary checks at milestone completion; reuse evidence only when code and dependencies remain unchanged. Rerun affected cases after fixes and the full suite at release. Record commit, environment, fixtures, expected/actual results, defects, and evidence. Failed prerequisites block dependent milestones; independent work may continue.

Keep GitHub issue dependencies and acceptance evidence synchronized. Never mark implementation complete from documentation alone. Maintain a restart bookmark every 30 minutes during execution.
