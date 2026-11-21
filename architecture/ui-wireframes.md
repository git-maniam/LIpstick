# Windows Modern UI Specifications & Wireframes: LI-pstick

## Visual Style Guide
- **Application Name:** `LI-pstick`
- **Shell Design:** Windows 11 Fluent Design System with WinUI 3 / Windows App SDK, Mica material backdrop, and rounded window frames.
- **Theme:** Light Theme as Default (`#F9F9F9`), Dark Theme Toggle (`#202020`).
- **Accent Color:** LinkedIn Professional Blue (`#0A66C2`) with Lip Stick polish aesthetics.

---

## Screen Layouts

### 1. Navigation Shell
```text
+----------------------------------------------------------------------------------------------------+
| [LI-pstick Icon]   LI-pstick                         [Model: Gemini 2.0 Flash v] [Theme: Sun/Moon] |
+------------------+---------------------------------------------------------------------------------+
|  (Nav View)      |                                                                                 |
|  [+] Connect     |  Active Tab Workspace                                                           |
|  [*] Scan        |                                                                                 |
|  [<>] Diff View  |                                                                                 |
|  [#] Content Lab |                                                                                 |
|  [@] History     |                                                                                 |
|                  |                                                                                 |
|  [?] Settings    |                                                                                 |
+------------------+---------------------------------------------------------------------------------+
```

### 2. Connect Tab (`/connect`)
- Three cards:
  1. **LinkedIn Account Connection Card**: Sign-in with LinkedIn (OAuth 2.0 / WebView2 session), shows current user name, avatar, connection state.
  2. **Google Gemini Connection Card**: Sign-in with Google or API Key input, displays account quota and available models (`Gemini 2.5 Flash`, `Gemini 2.0 Flash`, `Gemini 1.5 Pro`).
  3. **OpenAI Connection Card**: API Key input, model list (`GPT-4o`, `o3-mini`).

### 3. Scan Tab (`/scan`)
- Header banner with **Scan Profile** primary button.
- 100-point radial score ring showing total score and category scores.
- 12 rubric item cards with point breakdown and "Fix First" indicators.

### 4. Side-by-Side Diff Tab (`/diff`)
- Sub-navigation pills: `Headline`, `About Section`, `Experience Entries`, `Featured Items`.
- Left pane: Read-only live LinkedIn content with flagged issues.
- Right pane: Editable recommended rewrite with live Humanizer score pill (`88 PASS`), heuristic metric breakdown, and **[ APPLY TO LINKEDIN ]** action button.
