# LIpstick Functional Test Plan

This plan converts `SPEC.md`, the API contracts, wireframes, data files, and the ten Project LIpstick tickets into executable acceptance criteria. Tests use deterministic fakes for LinkedIn, Gemini, OpenAI, WebView2, filesystem, and notification services unless a case explicitly says otherwise.

## Test gates

- **T1 Build and launch:** Release/package build succeeds on the supported Windows target; the app launches without an unhandled exception.
- **T2 No secret leakage:** Test logs, config, crash output, and diagnostics contain no API keys, cookies, refresh tokens, or private profile text unless the case explicitly requires it.
- **T3 Data safety:** Failed, cancelled, interrupted, or malformed operations preserve the last valid local data and never perform an unintended LinkedIn write.
- **T4 Accessibility:** Every actionable control has an accessible name, keyboard focus is visible and logical, and all primary workflows work with keyboard navigation and screen-reader automation.
- **T5 Thresholds:** Humanizer PASS is `overall >= 70` and every individual metric is `>= 55`; any violation produces Review/Flagged and blocks Apply.
- **T6 External-write gate:** No Content Lab action publishes, sends, or changes LinkedIn without an explicit user action and confirmation.

## Functional cases

### Shell, navigation, and settings (issues #1 and #9)

| ID | Preconditions and steps | Expected result |
|---|---|---|
| SH-01 | Launch a clean install. | LIpstick opens to Connections; logo, title bar, Mica, Fluent surfaces, and Light theme are visible. |
| SH-02 | Navigate through Connections, Scanner, Recommendations, Content Lab, History, and Settings; collapse and expand NavigationView. | Each route loads without exception; selected route and back navigation remain correct. |
| SH-03 | Switch Light, Dark, and System themes; restart after each selection. | Colors, title bar, controls, and flyouts update immediately and the selected preference survives restart. |
| SH-04 | Use only keyboard to reach every navigation item, model selector, theme control, primary action, dialogs, and editor. | Focus order is logical, focus is visible, and no control is unreachable. |
| SH-05 | Resize from minimum supported window to maximized; test 125% and 150% DPI and High Contrast. | Content remains usable; no clipped primary actions, overlapping panes, or unreadable text. |
| ST-01 | Corrupt or truncate `config.json`, `voice.md`, `plan.md`, or `history.json`, then restart. | App recovers with defaults, preserves valid records where possible, reports the affected file, and never crashes. |
| ST-02 | Invoke Clear Local Data and cancel, then confirm. | Cancel changes nothing; confirmation removes only documented local artifacts and reports exactly what was removed. |

### LinkedIn browser login and user-provided profile snapshots (issue #2)

| ID | Preconditions and steps | Expected result |
|---|---|---|
| LI-01 | Click Open LinkedIn Login on a Windows desktop. | The default browser opens `https://www.linkedin.com/login`; browser credential management remains in control. |
| LI-02 | Enter valid and invalid profile URLs and restart the app. | Only LinkedIn member URLs are accepted and valid URLs persist locally. |
| LI-03 | Paste a profile snapshot with headline, About, Experience, Education, and Skills, then scan. | The app parses available sections, evaluates the snapshot, and preserves source text without invented identity fields. |
| LI-04 | Paste an updated snapshot after changing profile text and scan again. | The new local snapshot replaces the prior scan input and yields updated profile values/scores. |
| LI-05 | Inspect app config and secure storage. | No LinkedIn password, cookie, or browser token is collected or stored; profile URL and text remain local. |
| LI-06 | Trigger Apply on a recommendation. | LIpstick never claims to write to LinkedIn; user is told to apply manually and rescan a fresh snapshot. |

### AI providers and generation (issue #3)

| ID | Preconditions and steps | Expected result |
|---|---|---|
| AI-01 | Configure valid mocked Gemini credentials; list models and generate a recommendation. | Official adapter returns supported models, generated text, usage/latency metadata, and cancellation support through `IAIProvider`. |
| AI-02 | Repeat AI-01 for OpenAI. | OpenAI adapter behaves identically at the contract boundary. |
| AI-03 | Use invalid key, quota exceeded, unsupported model, malformed response, timeout, and cancellation. | Typed provider error maps to actionable UI state; no crash, secret exposure, or partial recommendation is presented. |
| AI-04 | Change provider/model in title-bar selector and restart. | Provider badge, model, context capacity, and latency estimate update immediately and persist without storing keys in config. |
| AI-05 | Generate with profile text, voice profile, and rubric criteria. | Captured request contains all required context and selected model; no unrelated private data is included. |

### Scanner and rubric (issue #4)

| ID | Preconditions and steps | Expected result |
|---|---|---|
| SC-01 | Scan perfect, empty, partial, malformed, and boundary fixture profiles. | All 12 categories render; total is deterministic and never exceeds 100; evidence and missing signals are shown. |
| SC-02 | Verify category maximums: 12/10/10/8/6/14/10/8/8/6/4/4. | Maximum total is exactly 100 and each category displays its correct weighting. |
| SC-03 | Complete scan, cancel mid-request, retry, and scan offline. | Progress/cancel work; last valid result remains visible; stale/offline state is explicit. |
| SC-04 | Inspect fix-first ordering for a fixture with multiple losses. | Highest-loss sections are ordered deterministically with actionable indicators. |
| SC-05 | Scan repeatedly without applying recommendations. | Scanner performs no LinkedIn mutation. |

### Humanizer and detector (issue #5)

| ID | Preconditions and steps | Expected result |
|---|---|---|
| HU-01 | Pass text containing U+200B, U+200D, U+2060, U+FEFF, U+00AD, and tag spaces. | All configured invisible characters are removed and the result is auditable. |
| HU-02 | Pass em dash, en dash, curly quotes, and ellipsis examples. | Typography normalizes to comma, hyphen, straight quotes, and three periods without damaging ordinary text. |
| HU-03 | Pass every `data/slop.json` entry, including delve, testament to, in the realm of, spearhead, seamless. | Configured terms are scrubbed or flagged according to rule; meaning is preserved and all changes are reported. |
| HU-04 | Evaluate high-quality, low-quality, and threshold-boundary text. | Five metrics, overall score, verdict, and structural tells are returned; threshold behavior matches T5 exactly. |
| HU-05 | Run `CleanText` twice and evaluate empty, Unicode-heavy, and long input. | Cleaning is idempotent, bounded, cancellable where applicable, and does not introduce fabricated claims. |
| HU-06 | Supply text below overall 70 or any metric below 55. | Bounded rewrite loop runs or returns Review/Flagged; it never loops indefinitely and original draft remains recoverable. |

### Recommendations and diff (issue #6)

| ID | Preconditions and steps | Expected result |
|---|---|---|
| DF-01 | Open each Headline, About, Experience, and Featured tab with a scanned profile. | Left pane is read-only live content; right pane is editable recommendation with accessible labels. |
| DF-02 | Compare text with removed buzzwords and added numbers. | Raw diff and formatted diff mark removals red, additions green, and do not mutate source text. |
| DF-03 | Type rapidly in the editor, then pause; switch tabs with unsaved edits. | Debounced live score updates; cancellation prevents stale results; edits persist until explicit discard. |
| DF-04 | Click Re-Generate after manually editing. | Confirmation prevents silent overwrite; selected provider/model is retained. |
| DF-05 | Hover/focus Humanizer pill. | Overall verdict and all five metric values are available through tooltip and accessible description. |

### Apply, history, and rollback (issue #7)

| ID | Preconditions and steps | Expected result |
|---|---|---|
| AP-01 | Attempt Apply while disconnected, below threshold, or with no changed text. | Apply is disabled or blocked with a precise reason; LinkedIn receives no request. |
| AP-02 | Approve a passing headline change and confirm dialog. | Old value is atomically backed up before update; update succeeds; success toast names section and backup. |
| AP-03 | Apply All with one success and one server failure. | Each section has an explicit status; successful backup remains; no false all-success notification appears. |
| AP-04 | Interrupt history write, corrupt history, or restart during write. | Atomic recovery preserves prior valid entries and never loses the last known good snapshot. |
| AP-05 | Roll back a selected historical version and cancel/confirm. | Cancel changes nothing; confirm restores selected content, records inverse history entry, and reports result. |
| AP-06 | Retry the same update and simulate timeout/rate limit. | Operation is idempotent or safely rejected; retries obey policy and never duplicate history incorrectly. |

### Content Lab (issue #8)

| ID | Preconditions and steps | Expected result |
|---|---|---|
| CL-01 | Open all 11 skill surfaces. | Each skill is reachable and exposes documented inputs and output shape. |
| CL-02 | Run post generation with thin idea, missing number, and valid idea. | Thin input requests specifics; missing metrics are never fabricated; valid output has three hooks, one draft, humanization receipt, and copy-ready block. |
| CL-03 | Run weekly plan with audience timezone and facts. | Four-post mix, non-repeating types, angles, hook IDs, timezone-aware times, and 5/3/2 engagement list are produced. |
| CL-04 | Run DM, inbox, comment, reply, profile, audit, carousel, and repurpose fixtures. | Each output follows its skill constraints, character limits, categories, and humanization requirements. |
| CL-05 | Attempt publish/send/log without explicit confirmation. | No external action occurs and no post is logged; explicit confirmation is the only gate. |
| CL-06 | Use outbound link, four hashtags, fabricated metric, or AI tell. | Validator flags or corrects the violation before copy-ready output. |

### Release and resilience (issue #10)

| ID | Preconditions and steps | Expected result |
|---|---|---|
| RE-01 | Execute mock end-to-end flow: connect → scan → generate → humanize → edit → apply → history → rollback. | Workflow completes with expected state transitions, evidence, and no unintended external writes. |
| RE-02 | Run all contract tests against deterministic provider and LinkedIn fakes. | Interfaces match API contracts and failures map consistently. |
| RE-03 | Kill/restart during network request, editor scoring, and file replacement. | App restarts safely, valid data remains, and pending work is clearly marked. |
| RE-04 | Run secret scan over source, test fixtures, config, logs, and packaged output. | No real credentials or unredacted sensitive data are present. |
| RE-05 | Run accessibility, DPI, High Contrast, resize, theme, and keyboard suite. | T1-T4 pass with recorded evidence and no blocking accessibility defects. |
| RE-06 | Produce Release build/package and launch on supported Windows target. | Package installs, launches, navigates, and completes smoke workflow without unhandled errors. |

## Traceability and evidence

Record for every case: build/commit, OS version, test data fixture, provider/LinkedIn fake version, timestamp, result, evidence link or screenshot, and defect ID. A phase is accepted only when its cases pass and T1-T6 are satisfied. Tests that require real provider credentials or LinkedIn access must use a separately approved sandbox account and must never run against a user's live profile during automated validation.
