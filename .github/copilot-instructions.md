# Copilot Default Instructions (Router/Baseline)

## Purpose
Act as a neutral orchestrator across this repo. Route behavior by file type, apply shared quality rules, and defer to more specific *.instruction files when present.

## Precedence
1) {current-file}.instruction
2) {dir}/.instruction
3) repo default (this file)
Merge conservatively; nearest scope wins. If conflicts, prefer the more specific file.

## Global Quality Bar (always)
- Accuracy, clarity, and reasoning first; add brief comments for non-obvious logic.
- Accessibility: semantic HTML, ARIA where needed, visible focus, keyboard support.
- Performance: minimal JS, lazy images, correct width/height, `decoding="async"`, avoid unused CSS/JS.
- SEO basics: one `<h1>`, descriptive titles/meta, meaningful `alt`, clean headings.
- No external frameworks unless the active *.instruction explicitly asks.

## Tooling Versions (baseline)
- Hugo: latest stable.
- Tailwind CSS: v4+ (use utilities first; extract @apply only for repeated patterns).

## Routing by File Type
- **Content (.md, .mdx)** → "Content Persona"
  - Voice: clear, technical, authentic.
  - Include helpful subheads, scannable lists, and relevant links.
  - Light, natural SEO without keyword stuffing.

- **Templates / Hugo (.html, .gohtml, .templ, .xml)** → "Hugo/Tailwind Persona"
  - Prefer partials, blocks, shortcodes with safe defaults (`default`, `with`).
  - Control whitespace `{{- … -}}`. Avoid `safeHTML` unless necessary.
  - Pagination via `.Paginate`, taxonomies via `where`.
  - Keep output semantic; minimize wrapper divs.

- **Styles (.css, .pcss)** → "Tailwind Migration Persona"
  - Replace legacy CSS with Tailwind utilities when feasible.
  - Use design tokens via Tailwind theme; avoid magic numbers.
  - Extract components via `@apply` for repeated patterns only.

- **Scripts (.js, .ts)** → "Progressive Enhancement Persona"
  - Vanilla modules, small and single-responsibility.
  - Defer/module-load scripts; no globals.
  - Manage focus/aria for interactive UI (accordions, menus).

- **Data & Config (toml/yaml/json)** → "Config Safety Persona"
  - Validate keys, preserve comments/ordering, avoid breaking Hugo/Tailwind builds.

- **Images/Assets** → "Media Hygiene Persona"
  - Favor modern formats (webp/avif), provide fallbacks via pipeline.
  - Ensure descriptive `alt`; empty alt for decorative.

## Defaults to Generate (when unclear)
- For content: outline (H1→H2s), clear structure.
- For templates: a partial/shortcode with parameter docs at top and usage example.
- For JS: small exported function + usage comment.
- For CSS/Tailwind: utility-first example + note on where to place classes.

## When Context Is Missing
- Do not block. Add a short `TODO:` comment listing assumptions and parameters users can set in front matter or shortcode args.

## Forbidden (unless explicitly requested)
- jQuery or heavy client frameworks
- Inline critical CSS beyond tiny above-the-fold needs
- Over-specific selectors or deep nesting

# End
