---
title: Documentation Style Guide
description: Conventions for writing and maintaining Vecerdi.Extensions.DependencyInjection documentation.
order: 99
tags: [meta]
---

# Documentation style guide

The contract for every article under `Documentation~/`, shared with the other Vecerdi.* packages' documentation so
they read alike. Files starting with `_` are meta and never linked from content.

## Frontmatter

```yaml
---
title: Lifecycle                # matches the H1
description: One sentence for index pages. Ends with a period.
order: 4                        # position in the index (README.md is 0)
tags: [lifecycle]               # lowercase, 1-4, reuse before inventing
sources:                        # the staleness contract: when these change, the article is suspect
  - ServiceManager.cs           # package-relative files or folders (trailing /)
---
```

List the paths you actually read while writing. Never reference sibling packages or the host application.

## Structure and voice

- The H1 equals the title; the first paragraph says what the feature is and when to reach for it.
- `##` sections, `###` sparingly, never `####`; sentence-case headings without HTML entities (they break anchors).
- Second person, present tense, active voice. No marketing filler, no hedging unless the caveat is real.
- Paragraphs of 2–5 sentences; bullets for genuinely enumerable facts, tables for members and rules.
- End with a short **See also** when there are natural next steps.
- Alerts (`> [!NOTE]`, `> [!TIP]`, `> [!WARNING]`) sparingly, a few per article at most.

## Code samples

- **Never invent APIs.** Every package identifier must exist in the source; adapt real usage where possible.
  Consumer-side names (`ScoreService`, `AudioDirector`) are fine as stand-ins.
- File-scoped namespaces, `var`, 4-space indent. Show the `using`s a consumer needs: the package's `GlobalUsings.cs`
  doesn't apply to their code.
- Keep samples minimal but valid C#: a reader should be able to paste one and adjust names.

## Accuracy

If a behavior can't be verified from the source, the tests or a live check, leave it out, or mark the open question
with a `<!-- TODO: verify ... -->` comment.

## Terminology

| Term | Meaning |
|---|---|
| the container | The service provider `ServiceManager` builds for a play session |
| session | One play session (editor) or run (player): the container's lifetime |
| code lifetime | One load of the code, from `Vecerdi.Extensions.Lifetime`; spans many sessions with domain reload off |
| component service | A `MonoBehaviour` registered with an `AddMonoBehaviour*` extension |
| context | A `TypeInjectorResolverContext` the source generator fills in |
| the package | Vecerdi.Extensions.DependencyInjection (avoid "the library") |
