# CSS support in BrowserCore's Custom Engine

The current custom renderer focuses on extracting plain text, basic imagery, and a handful of inline background hints from fetched HTML.

## What the engine currently understands
- **Inline background styles.** The parser scans `style="..."` attributes for `background-image`, `background-size`, `background-position`, `background-repeat`, and an optional `height` to approximate hero images when recreating content with XAML elements.【F:BrowserCore/Engine/CustomHtmlEngine.cs†L1317-L1372】

## Key CSS capabilities missing compared with Chromium, Firefox, or Safari
- **External and embedded stylesheets.** All `<style>` blocks are stripped before content extraction, and there is no code to download or apply `<link rel="stylesheet">` resources, so the cascade never runs.【F:BrowserCore/Engine/CustomHtmlEngine.cs†L212-L219】
- **General CSS property support.** Beyond the background hints above, inline declarations are ignored; layout is rebuilt using simple `StackPanel`, `TextBlock`, and `Border` controls without interpreting CSS boxes, positioning, typography, or media queries.【F:BrowserCore/Engine/CustomHtmlEngine.cs†L199-L204】【F:BrowserCore/Engine/CustomHtmlEngine.cs†L1231-L1339】
- **CSS-driven interactivity and effects.** There is no engine for transitions, animations, transforms, filters, or pseudo-classes because styles are neither parsed nor applied, leaving features like hover effects, sticky positioning, and blend modes unsupported.【F:BrowserCore/Engine/CustomHtmlEngine.cs†L212-L219】【F:BrowserCore/Engine/CustomHtmlEngine.cs†L1317-L1372】

Modern browsers evaluate the full CSS specification—including cascading order, selector matching, layout modules (Flexbox, Grid), font loading, animations, and compositing—which are absent in the current custom engine. As a result, pages that depend on sophisticated CSS will render far richer in Chromium, Firefox, or Safari than in BrowserCore's simplified reader.
