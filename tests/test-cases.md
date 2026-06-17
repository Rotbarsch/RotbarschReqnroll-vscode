# Reqnroll VS Code Extension – Test Cases

All tests are automated via Playwright (see `tests/` folder). They drive the
locally-installed VS Code as an Electron app — no browser download required.
The `Demo/Example.NUnit` project is used as the test workspace.

Run the suite with:
```powershell
.\tests\run-tests.ps1               # headless (default)
.\tests\run-tests.ps1 -Headed       # show VS Code window
.\tests\run-tests.ps1 -Test "syntax" # filter by name
```

---

## 1. Syntax Highlighting (`syntax-highlighting.spec.ts`)

### 1.1 Language Detection

| # | Test | Feature file | Expected |
|---|---|---|---|
| 1.1.1 | Feature file language detected | `FirstFeature.feature` | Status bar shows "Reqnroll Feature" |
| 1.1.2 | Special-chars file opens without crash | `SpecialChars.feature` | Editor visible; apostrophe content rendered |

### 1.2 Keywords visible in editor

#### `FirstFeature.feature`

| # | Test | Expected |
|---|---|---|
| 1.2.1 | `Feature:` keyword | Visible in editor |
| 1.2.2 | `Scenario:` keyword | Visible in editor |
| 1.2.3 | `When` step keyword | Visible in editor |
| 1.2.4 | `Then` step keyword | Visible in editor |
| 1.2.5 | Tag `@firstTest` | Visible in editor |

#### `NumbersOutline.feature`

| # | Test | Expected |
|---|---|---|
| 1.2.6 | `Scenario Outline:` keyword | Visible in editor |
| 1.2.7 | `Examples:` keyword | Visible in editor |
| 1.2.8 | Outline parameter `<summand1>` | Visible in editor |

#### `SyntaxShowcase.feature`

| # | Test | Expected |
|---|---|---|
| 1.2.9  | `Background:` keyword | Visible in editor |
| 1.2.10 | `Given` step keyword | Visible in editor |
| 1.2.11 | Comment line `# This feature file exercises…` | Visible in editor |
| 1.2.12 | Triple-quoted docstring `"""` | Visible in editor |
| 1.2.13 | Double-quoted string `"hello"` in scenario name | Visible in editor |
| 1.2.14 | Single-quoted string argument `'hello'` | Visible in editor |
| 1.2.15 | Tags `@syntax` and `@showcase` | Visible in editor |

### 1.3 Grammar active

| # | Test | Expected |
|---|---|---|
| 1.3.1 | Coloured syntax tokens | Monaco editor has > 0 spans with colour token classes (`mtkN`, N > 1) |

### 1.4 Token Colours

Monaco assigns CSS class `mtk1` to plain (unstyled) text and `mtkN` (N > 1) to
theme-coloured tokens.  These tests verify that each Gherkin token type is
highlighted with a non-default colour **and** that different token types receive
**different** colour classes from each other.

#### `FirstFeature.feature` token colours

| # | Test | Expected |
|---|---|---|
| 1.4.1 | `Feature:` keyword | `mtkN` class where N > 1 |
| 1.4.2 | `Scenario:` keyword | `mtkN` class where N > 1 |
| 1.4.3 | Tag `@firstTest` | `mtkN` class where N > 1 |
| 1.4.4 | `Feature:` vs `@firstTest` colour difference | Different `mtkN` class |

#### `NumbersOutline.feature` token colours

| # | Test | Expected |
|---|---|---|
| 1.4.5 | Outline parameter `<summand1>` | `mtkN` class where N > 1 |

#### `SyntaxShowcase.feature` token colours

| # | Test | Expected |
|---|---|---|
| 1.4.6 | Comment line | `mtkN` class where N > 1 |
| 1.4.7 | Docstring delimiter `"""` | `mtkN` class where N > 1 |
| 1.4.8 | Single-quoted string `'hello'` | `mtkN` class where N > 1 |
| 1.4.9 | `Feature:` vs comment: different colours | Different `mtkN` class |
| 1.4.10 | `Feature:` vs `@syntax` tag: different colours | Different `mtkN` class |

---

## 2. Commands (`commands.spec.ts`)

### 2.1 Command Palette – Registration

Verifies that each command is registered and discoverable via Ctrl+Shift+P.

| # | Test | Expected |
|---|---|---|
| 2.1.1 | `Reqnroll: Rebuild project` | Command appears in palette |
| 2.1.2 | `Reqnroll: Rebuild project (full)` | Command appears in palette |
| 2.1.3 | `Reqnroll: Re-run test discovery` | Command appears in palette |
| 2.1.4 | `Reqnroll: Refresh bindings` | Command appears in palette |

### 2.2 Command Execution – Notifications

Verifies that triggering a command via the editor context menu produces a
visible result notification toast.  A .feature file must be open for the
extension's `when` clause (`resourceExtname == .feature`) to activate the menu.

| # | Test | Expected |
|---|---|---|
| 2.2.1 | `Reqnroll: Rebuild project` via context menu | Notification toast containing "Reqnroll" appears (within 90 s) |
| 2.2.2 | `Reqnroll: Refresh bindings` via context menu | Notification toast containing "binding" appears (within 90 s) |

---

## 3. Context Menu (`context-menu.spec.ts`)

### 3.1 Editor Context Menu

Right-click inside the Monaco editor while `FirstFeature.feature` is active.
The extension contributes commands via `"menus": { "editor/context": [...] }`
with `"when": "resourceExtname == .feature"`.

| # | Test | Expected |
|---|---|---|
| 3.1.1 | At least one Reqnroll command visible | Any `Reqnroll` item in context menu |
| 3.1.2 | `Reqnroll: Rebuild project` individually | Exact label visible |
| 3.1.3 | `Reqnroll: Rebuild project (full)` individually | Exact label visible |
| 3.1.4 | `Reqnroll: Re-run test discovery` individually | Exact label visible |
| 3.1.5 | `Reqnroll: Refresh bindings` individually | Exact label visible |

### 3.2 Explorer Context Menu

Right-click `FirstFeature.feature` in the Explorer sidebar.  The test skips
gracefully if VS Code has not expanded the folder tree to show the file.

| # | Test | Expected |
|---|---|---|
| 3.2.1 | At least one Reqnroll command visible | Any `Reqnroll` item in Explorer context menu |

---

## 4. Test Explorer & Execution (`test-explorer.spec.ts`)

> Workspace: `Demo/Example.NUnit` — must be built before running.

### 4.1 Panel

| # | Test | Expected |
|---|---|---|
| 4.1.1 | Test Explorer panel can be opened | Tree rows visible in ≤ 30 s |

### 4.2 Discovery

| # | Test | Expected |
|---|---|---|
| 4.2.1 | Tests discovered from feature files | At least one tree row in ≤ 60 s |
| 4.2.2 | Scenario "Addition" visible by name | Row with text "Addition" in tree |
| 4.2.3 | SubFolder hierarchy node visible | Row with text "SubFolder" in tree |

### 4.3 Test Execution

| # | Test | Expected |
|---|---|---|
| 4.3.1 | Run a single scenario | Result icon (pass/fail/error) appears next to item |
| 4.3.2 | Run All Tests | Result icons appear; VS Code stays responsive |

### 4.4 Filter

| # | Test | Expected |
|---|---|---|
| 4.4.1 | Typing in filter input narrows visible tests | Row count after typing "Addition" < row count before; **skipped automatically** if the filter toolbar button is not accessible in this VS Code version |

---

## 5. LSP Diagnostics (`diagnostics.spec.ts`)

> Workspace: `Demo/Example.NUnit` — must be built before running.
> A temporary feature file `_UnboundStep_Temp.feature` is created inside
> `Demo/Example.NUnit/Features/` before VS Code starts and deleted in `afterAll`.
> The file is opened using its **full absolute path** in VS Code's quick-open to
> avoid ambiguity with the auto-generated `.feature.cs` code-behind file.
> Both the `.feature` and the `.feature.cs` files are cleaned up on completion.

### 5.1 Unbound Steps

| # | Test | Expected |
|---|---|---|
| 5.1.1 | Warning squiggle for unbound step | `.squiggly-warning` element present in Monaco overlay, or (fallback) Problems panel has ≥ 1 row |
| 5.1.2 | Problems panel lists entry for temp file | At least one `.monaco-list-row` in Problems panel within 20 s |

---

## 6. Autocomplete – Ctrl+Space (`autocomplete.spec.ts`)

> Workspace: `Demo/Example.NUnit` — must be built before running.
> A temporary feature file `_Autocomplete_Temp.feature` is created inside
> `Demo/Example.NUnit/Features/` before VS Code starts and deleted in `afterAll`.
> The file contains one blank indented line (line 6) inside a `Scenario:` block
> where typing tests insert text, trigger completion, then restore the line.

Trigger characters registered by the completion handler: `G W T A B F S E`
(first letters of Gherkin keywords).

### 6.1 Widget visibility

| # | Test | Expected |
|---|---|---|
| 6.1.1 | Ctrl+Space on blank indented line inside scenario | `.suggest-widget` becomes visible |

### 6.2 Context-aware keyword suggestions

| # | Test | Expected |
|---|---|---|
| 6.2.1 | Blank indented line inside a Scenario | Suggestions include `Given`, `When`, `Then`, `And`, `But` |
| 6.2.2 | Blank unindented line outside any block | Suggestions include `Scenario`, `Feature`, `Background` |

### 6.3 Step-definition suggestions from bound bindings

| # | Test | Expected |
|---|---|---|
| 6.3.1 | After typing `When ` | Bindings matching `When` are listed (`I add`, `is appended with`) |
| 6.3.2 | After typing `Then ` | Bindings matching `Then` are listed (`the result should be`) |
| 6.3.3 | After typing `Given ` | Bindings matching `Given` are listed (`the system is ready`, `the following message:`) |

### 6.4 Acceptance

| # | Test | Expected |
|---|---|---|
| 6.4.1 | Pressing Enter on highlighted suggestion | Step text is inserted into the editor line |

---

## 7. Document Outline (`outline.spec.ts`)

> Workspace: `Demo/Example.NUnit` — must be built before running.

The Reqnroll LSP server exposes the Gherkin structure of each open feature file
as LSP document symbols (`textDocument/documentSymbol`).  VS Code uses these
symbols in two ways: the **Outline panel** in the Explorer sidebar (collapsible
section at the bottom) and the **Go to Symbol** quick-pick (Ctrl+Shift+O).

Symbol kinds produced by the handler:

| Gherkin keyword | LSP `SymbolKind` | Outline label suffix |
|---|---|---|
| `Feature:` | `Module` | `(module)` |
| `Background:` | `Event` | `(event)` |
| `Scenario:` | `Method` | `(method)` |
| `Scenario Outline:` | `Method` | `(method)` |
| `Examples:` | `Array` | `(array)` |

### 7.1 Outline panel (Explorer sidebar)

VS Code renders outline symbols as `role="treeitem"` elements with accessible
names in the format `"<symbolName> (<symbolKind>)"`.  The Outline section header
is a collapsible button with `aria-label="Outline Section"`.

| # | Test | Expected |
|---|---|---|
| 7.1.1 | `Feature:` node for `FirstFeature.feature` | `treeitem "FirstFeature (module)"` visible after expanding Outline section |
| 7.1.2 | Scenario nodes as children of Feature | `treeitem "Addition (method)"` and `"Another Addition (method)"` visible |
| 7.1.3 | `Background:` node for `SyntaxShowcase.feature` | `treeitem "Common setup (event)"` visible |
| 7.1.4 | `Scenario Outline:` node for `NumbersOutline.feature` | `treeitem "Addition with Examples (method)"` visible |

### 7.2 Go to Symbol in Editor (Ctrl+Shift+O)

| # | Test | Expected |
|---|---|---|
| 7.2.1 | Ctrl+Shift+O opens the symbol quick-pick | `.quick-input-widget` becomes visible |
| 7.2.2 | Feature name in symbol list | Quick-pick row contains "FirstFeature" |
| 7.2.3 | Scenario names in symbol list | Quick-pick rows contain "Addition" and "Another Addition" |
| 7.2.4 | Selecting a symbol navigates editor to correct line | After selecting "Another Addition", editor shows that text |
| 7.2.5 | `Background:` symbol for `SyntaxShowcase.feature` | Quick-pick row contains "Common setup" |
| 7.2.6 | `Examples:` symbol for `NumbersOutline.feature` | Quick-pick row contains "Examples" |

---

## Feature files used as test fixtures

All `.feature` files live in `Demo/Example.NUnit/Features/` and are mirrored to
`Example.MsTest`, `Example.XUnit2`, and `Example.XUnit3`.

| File | Gherkin elements demonstrated |
|---|---|
| `FirstFeature.feature` | `Feature`, `Scenario`, `When`, `Then`, tag `@firstTest` |
| `NumbersOutline.feature` | `Scenario Outline`, `Examples`, parameters `<…>` |
| `BoolFeature.feature` | `Scenario Outline`, boolean examples |
| `StringFeature.feature` | `Scenario Outline`, `$()` parameters |
| `SpecialChars.feature` | Apostrophes in scenario names and steps |
| `LongScenarioName.feature` | Very long scenario names |
| `SyntaxShowcase.feature` | `Background`, comments (`#`), docstrings (`"""`), double-quoted strings (`"…"`), single-quoted strings (`'…'`), tags |
| `SubFolder/BoolFeatureInSubfolder.feature` | Feature in subfolder (hierarchy) |
| `SubFolder/DeepFolder/DeepFeature.feature` | Feature three levels deep |

