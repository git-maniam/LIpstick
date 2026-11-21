# Windows Modern UI Specifications & Wireframes: LIpstick

## Visual Style Guide
- **Application Name:** `LIpstick`
- **Application Icon:** `LIpstick-Logo.png`
- **Shell Design:** Windows 11 Fluent Design System with WinUI 3 / Windows App SDK, Mica material backdrop, and rounded window frames.
- **Theme:** Light Theme as Default (`#F9F9F9`), Dark Theme Toggle (`#202020`).
- **Accent Color:** LinkedIn Professional Blue (`#0A66C2`) with Lipstick polish aesthetics.

---

## Screen Layouts

### Approved Supporting Screen Reference
See [`../Connections-Content-History-Settings.png`](../Connections-Content-History-Settings.png) for the approved visual direction for Connections, Content Lab, History, and Settings. It complements `Scanner.png` and `Improve.png`.

### 2. Connect Tab (`/connect`)
- Persistent sidebar shows LinkedIn profile-setup state plus OpenAI and Gemini connection states.
- Three provider cards show concise purpose, Connected/Disconnected status, and connect/disconnect action.
- Explain that at least one AI provider must be connected to populate the model selector.

### 3. Content Lab (`/content`)
- Provide a selectable tool list for Posts, Carousel, Weekly Plan, DM, Inbox, Comments, Replies, Profile, Audit, and Repurpose.
- Show the selected workflow's inputs beside its draft/preview and actions.
- Keep a visible notice that drafts are not automatically posted or sent.

### 4. History (`/history`)
- Provide section/date/status filters and a readable list of changes with previous/applied text summaries, score, and status.
- Restore requires a confirmation that describes the replacement.

### 5. Settings (`/settings`)
- Group appearance, AI provider/model defaults, privacy and local data location, export, and clear-data controls.
- Confirm destructive data deletion and explain what will be removed.

### Persistent Sidebar Account Controls
Every screen includes persistent sidebar controls for LinkedIn, OpenAI, and Gemini. LinkedIn opens the default browser login page and shows profile-setup status; the browser manages any remembered login. Scanner makes one unauthenticated request to the saved public profile URL per explicit scan, with pasted text as fallback. LIpstick does not use browser cookies or apply edits. The model selector lists models only for connected AI providers.

### 1. Navigation Shell
```text
+----------------------------------------------------------------------------------------------------+
| [LIpstick-Logo.png]   LIpstick                       [Model: Gemini 2.0 Flash v] [Theme: Sun/Moon] |
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
  1. **LinkedIn Profile Setup Card**: Default-browser login action, saved public profile URL field, and explanation that Scanner fetches public page content with pasted-text fallback.
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
