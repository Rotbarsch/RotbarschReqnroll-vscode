# RotbarschReqnroll-vscode
A VS Code extension specifically tailored to Reqnroll. 
Only Windows for now - sorry.

# Getting started
1. I recommend installing [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) for easy test running, if not already installed in your Visual Studio Code.
2. Install the Rotbarsch.Reqnroll Visual Studio Code extension
3. For best results, set the following in your VS Code settings.json:
```
"[reqnroll-feature]": {
        "editor.formatOnSave": true
    }
```

# Features

## Syntax Highlighting
- Full syntax highlighting for Reqnroll feature files
- Highlight step arguments in single quotes explicitly

## Document outline
- Supports VS Code Outline view, allowing quick navigation in long feature files.

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

# Debugging
Open this workspace in VS Code and (my recommendation) the .sln file in Visual Studio. When pressing F5 (using the "Run VS Code Extension") in VS Code, it builds the TypeScript client and C# Language Server Protocol Server in Debug mode. 
When the server is started in DEBUG configuration, you've got 10 seconds to attach a debugger (that's what the Visual Studio instance is for).

# Building as VSIX
1. In client (`src/client`) folder: `npm run compile`
2. In server (`src/server/Reqnroll.LanguageServer`) folder: `dotnet publish -c Release -r win-x64 --self-contained true`
3. Make sure the `src/client/artifacts/lsp` folder is filled with the build output. This should work through the way the csproj is set up.
4. Install the Visual Studio code extension packager via `npm install -g @vscode/vsce` (only once, of course)
5. Package the project, so execute in `src/client` the following: `vsce package`
6. Install the created vsix file: `code --install-extension .\rotbarsch-reqnroll-vscode-0.0.1.vsix`
7. If syntax highlighting and auto complete works, you're all set up! Check the "Reqnroll Language Server" in the output of your VS Code instance to check for errors.
