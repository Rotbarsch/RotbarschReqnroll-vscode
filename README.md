# RotbarschReqnroll-vscode
A VS code extensions specifically tailored to Reqnroll. 
Only Windows for now - sorry.

# Features

## Syntax Highlighting
- Full syntax highlighting for Reqnroll feature files
- Highlight step arguments in single quotes explicitly

## Intelligent Code Completion
- Auto-complete suggestions for step definitions based on your project's bindings
- Displays step descriptions and parameter information while typing
- Shows binding source (class and method) for each suggestion
- Smart keyword insertion (includes Given/When/Then in completion)
- Considers position in document to filter only for useful recommendations

## Diagnostics
- Real-time detection of steps without matching bindings
- Warning indicators for undefined steps
- Helps identify missing step implementations

## Hover Documentation
- Hover over any step to see detailed documentation
- View step descriptions, parameter details, regex patterns
- Quick access to binding source information (class and method)

## Code Formatting
- Automatic table formatting with aligned columns
- Smart indentation (steps indented by one tab)
- Consecutive keyword normalization (e.g., multiple "When" steps become "When...And...And...")
- Preserves proper Gherkin structure

## Binding Integration
- Loads step bindings from your project assemblies
- Caches binding metadata with automatic refresh (30-second intervals)
- Supports both regex and Cucumber expression patterns
- Extracts parameter information and documentation from XML comments

## Test Execution
- Run feature tests directly from the editor with CodeLens actions (requires [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit))
