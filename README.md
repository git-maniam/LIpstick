# LI-pstick: Windows Modern UI for LinkedIn

> **LI-pstick** (Lip Stick for LinkedIn) — makeup and polish for your professional LinkedIn presence. Built with latest **.NET 10 (C# 14)** and **Windows Modern UI (WinUI 3 / Windows App SDK)**.

This repository contains the complete specification, data dictionaries, heuristic engines, architecture documents, and .NET project scaffolding required to build **LI-pstick**.

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
   - Connect to LinkedIn account via embedded WebView2 / OAuth 2.0.
   - Scan button evaluates profile against the 100-point rubric in `data/rubric.json`.
4. **Side-by-Side Recommendations & Real-Time C# Humanizer:**
   - Side-by-side diff comparison between current profile and AI rewrite.
   - Real-time scoring and cleaning engine (invisible Unicode removal, typographic normalization, slop scrub) ensuring high human score ($\text{PASS} \ge 70$).
5. **Direct "Apply" Synchronizer:**
   - Apply button syncs the verified humanized text directly to the live LinkedIn profile.
   - Saves previous state to rollback history for safe undo.

---

## Development Prerequisites

- **.NET 10 SDK** (or higher)
- **Visual Studio 2026 / Visual Studio Code** with C# Dev Kit and Windows App SDK workloads
- Windows 11 (build 22000+) or Windows 10 (version 1809+, build 17763+)
