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
| 1.2.11 | Comment line `# This feature file demonstrates…` | Visible in editor |
| 1.2.12 | Triple-quoted docstring `"""` | Visible in editor |
| 1.2.13 | Double-quoted string `"hello"` in scenario name | Visible in editor |
| 1.2.14 | Tags `@syntax` and `@showcase` | Visible in editor |

### 1.3 Grammar active

| # | Test | Expected |
|---|---|---|
| 1.3.1 | Coloured syntax tokens | Monaco editor has > 0 spans with colour token classes (`mtkN`) |

---

## 2. Command Palette (`commands.spec.ts`)

| # | Test | Expected |
|---|---|---|
| 2.1 | `Reqnroll: Rebuild project` | Command appears in palette |
| 2.2 | `Reqnroll: Rebuild project (full)` | Command appears in palette |
| 2.3 | `Reqnroll: Re-run test discovery` | Command appears in palette |
| 2.4 | `Reqnroll: Refresh bindings` | Command appears in palette |

---

## 3. Context Menu (`context-menu.spec.ts`)

| # | Test | Expected |
|---|---|---|
| 3.1 | Right-click inside open `.feature` editor | Reqnroll commands visible in editor context menu |
| 3.2 | Right-click `.feature` file in Explorer | Reqnroll commands visible in Explorer context menu |

---

## 4. Test Explorer & Test Execution (`test-explorer.spec.ts`)

> Workspace: `Demo/Example.NUnit` — must be built before running.

### 4.1 Test Explorer

| # | Test | Expected |
|---|---|---|
| 4.1.1 | Open Test Explorer panel | Tree rows are visible (panel renders without crash) |
| 4.1.2 | Tests discovered from feature files | At least one test item visible in tree within 60 s |

### 4.2 Test Execution

| # | Test | Expected |
|---|---|---|
| 4.2.1 | Run a single scenario | Result icon (pass/fail) appears next to test item |
| 4.2.2 | Run All Tests | Result icons appear; VS Code remains open and responsive |

---

## Feature files used as test fixtures

All `.feature` files live in `Demo/Example.NUnit/Features/` and are mirrored to
`Example.MsTest`, `Example.XUnit2`, and `Example.XUnit3`.

| File | Gherkin elements demonstrated |
|---|---|
| `FirstFeature.feature` | `Feature`, `Scenario`, `When`, `Then`, tag `@firstTest` |
| `NumbersOutline.feature` | `Scenario Outline`, `Examples`, parameters `<…>` |
| `BoolFeature.feature` | `Scenario Outline`, boolean examples |
| `StringFeature.feature` | `Scenario Outline`, `$(…)` parameters |
| `SpecialChars.feature` | Apostrophes in scenario names and steps |
| `LongScenarioName.feature` | Very long scenario names |
| `SyntaxShowcase.feature` | `Background`, comments (`#`), docstrings (`"""`), double-quoted strings, tags |
| `SubFolder/BoolFeatureInSubfolder.feature` | Feature in subfolder (hierarchy) |
| `SubFolder/DeepFolder/DeepFeature.feature` | Feature three levels deep |
