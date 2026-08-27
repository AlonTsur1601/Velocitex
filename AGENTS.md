# Agent guide

<!-- mnemex:codex-guard:start -->
## Mnemex decision guard

Before editing a file, call `context_for` for that path.
Before applying a material change, call `check_proposed_change` with the path,
a concise patch summary, and constraint enforcement enabled.
If Mnemex blocks, do not apply the edit unless a human explicitly approves an
override; record the actor and reason with `override_decision_guard`.
After an accepted edit, call `index_path` for the changed path and reconcile any
stale cited decision rather than silently rewriting it.
Treat unavailable or uncertain semantic judgment as advisory.
<!-- mnemex:codex-guard:end -->
