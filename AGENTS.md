# AGENTS.md

## Status

Repository is currently **empty** (no manifests, build config, lockfiles, or git history as of 2026-10-18). Intended project: a **web widget that calls the Google Gemini API**.

Nothing below is verified against code yet. When you scaffold the project, replace the placeholders with real, verified commands — do not trust guesses.

## Required setup (do first, once files exist)

- Read the actual root manifest/config before running anything; there is no existing source of truth.
- Establish and record exact commands here:
  - install: `<TODO>`
  - dev server: `<TODO>`
  - build: `<TODO>`
  - lint / typecheck / test: `<TODO>`
  - single test: `<TODO>`

## Gemini API gotchas

- Never hardcode an API key. Widgets run in the browser, so any key embedded in client code is public. Route Gemini calls through a backend/proxy, or use a server-issued short-lived token.
- Keep the model name and API version in one place; they change and are easy to scatter.
- Do not commit real keys; add `.env*` to `.gitignore` when the manifest is created.

## Working conventions

- Update this file whenever you add a real command, entrypoint, or constraint that an agent would otherwise guess wrong.
