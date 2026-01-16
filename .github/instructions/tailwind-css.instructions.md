---
applyTo: '**'
---
# Tailwind CSS Build Pipeline

## Overview

This project uses **Tailwind CSS v4** with a build-time compilation pipeline. The CSS is generated during the .NET build process and served from the WebApp server's wwwroot.

## File Locations

| File | Location | Purpose |
|------|----------|---------|
| Source CSS | `src/Presentation.WebApp.Client/wwwroot/css/tailwind.css` | Tailwind directives + custom utilities |
| Generated CSS | `src/Presentation.WebApp.Client/wwwroot/css/tailwind.generated.css` | Compiled output (minified) |
| Served CSS | `src/Presentation.WebApp/wwwroot/tailwind.css` | Copy served by the server |
| Package config | `src/Presentation.WebApp.Client/package.json` | npm scripts for Tailwind CLI |

## How It Works

1. **Source file** (`tailwind.css`) contains:
   - `@tailwind base;` `@tailwind components;` `@tailwind utilities;` directives
   - Custom utility classes in `@layer utilities`

2. **Build target** in `Presentation.WebApp.Client.csproj` runs automatically:
   ```xml
   <Target Name="BuildTailwindCss" BeforeTargets="Build">
       <Exec Command="npm install" Condition="!Exists('node_modules')" />
       <Exec Command="npm run tailwind:build" />
   </Target>
   ```

3. **npm scripts** in `package.json`:
   - `npm run tailwind:build` - One-time build (minified)
   - `npm run tailwind:watch` - Watch mode for development

4. **Generated CSS** is scanned from `**/*.razor`, `**/*.cshtml`, `**/*.html` files

## Adding New Tailwind Classes

When adding new Tailwind utility classes to Razor components:

1. Just use the class in your `.razor` file - Tailwind CLI scans all Razor files
2. Run `dotnet build` to regenerate CSS, or use watch mode during development
3. The build target handles everything automatically

## Development Workflow

For active CSS development with hot reload:

```powershell
cd src/Presentation.WebApp.Client
npm run tailwind:watch
```

This watches for changes and regenerates `tailwind.generated.css` automatically.

## Custom Utilities

Add custom styles in the source `tailwind.css` file under `@layer utilities`:

```css
@layer utilities {
    .my-custom-class {
        /* styles */
    }
}
```

## Important Notes

- **Never edit `tailwind.generated.css`** - It's auto-generated and will be overwritten
- The CSS is served from the **server's wwwroot**, not the client's `_content/` path
- Tailwind v4 uses the new CSS-first configuration (no `tailwind.config.js` needed)
- The `@tailwindcss/cli` package is used for compilation

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Classes not applying | Run `dotnet build` to regenerate CSS |
| Missing node_modules | Run `npm install` in `Presentation.WebApp.Client` |
| CSS not updating | Check that `tailwind:build` ran (see build output) |
| New class not included | Ensure the class is used in a `.razor`/`.cshtml`/`.html` file |
