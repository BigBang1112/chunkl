---
name: update-github-action-versions
description: Check upstream GitHub Action releases and update repository action references to the latest stable major version tags, such as v6 or v7. Use when asked to update action versions or refresh workflow dependencies.
---

# Update GitHub Action versions

Use the latest stable major version tag for each versioned action. For example, a latest release of `v7.2.3` should produce `owner/action@v7`.

## Find references

Scan repository workflows, local action definitions, and relevant workflow examples for `uses:` references. Include both `.yml` and `.yaml` files. Exclude dependency folders, build output, and Git internals.

Group references by upstream repository so each action is checked once. Preserve subpaths in references such as `owner/repo/path/to/action@vN`.

## Verify current releases

- Check the upstream repository's latest stable release using its official release page or GitHub API. For example, `gh api repos/OWNER/REPO/releases/latest --jq .tag_name`. Verify live information instead of relying on remembered versions.
- Exclude draft and prerelease releases unless the user explicitly requests them.
- Extract the major number from the release tag and verify that the upstream publishes the corresponding `vN` tag. The GitHub API endpoint `repos/OWNER/REPO/git/ref/tags/vN` can check its existence. Account for annotated tags when comparing commits.
- If the major tag trails the latest stable release, use the requested major tag and report that upstream discrepancy.
- If the action has no stable releases, use its documented versioning convention. Keep supported channel references such as `dtolnay/rust-toolchain@stable`. Do not invent `vN` tags for actions or reusable workflows that use branches, channels, or another tag scheme.
- If upstream information cannot be verified, retain the reference and report what remains unchecked.

## Update and verify

Read release notes for major upgrades and adjust action inputs or permissions only when needed for compatibility with the existing workflow.

Update every applicable reference in the requested scope to `@vN`. Use major tags rather than minor tags, full release versions, `latest`, or commit hashes unless the user explicitly requests another format. Preserve local `./...` references and Docker references; they do not use this action tag scheme.

Keep runner choices, toolchain versions, workflow triggers, job dependencies, and unrelated settings intact unless an action upgrade requires a change.

Parse changed YAML, review the diff, and check for missed references. Summarize the selected major tags with links to their upstream releases, any compatibility changes, and references that could not be verified or use another versioning convention.
