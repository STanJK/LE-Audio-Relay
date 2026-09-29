# LE Audio Relay VF-KB

This directory is the current semantic projection of the active LE Audio Relay implementation.

- Schema: `vf-kb/0.1`
- Canonical implementation snapshot for this refresh: `da3217f76a0632bdaf6fea25e9103dbef0298137`
- Scope: current Round4 product/runtime semantics only
- Historical V0.1 evidence remains frozen under `Legacy/V0.1/VF/`

> The node IDs intentionally retain the historical `leaudio-router.*` prefix. IDs are stable knowledge identifiers, not current product branding.

## Current graph

```text
leaudio-router.round4-shell
├─ endpoint-lifecycle
├─ power-lifecycle
├─ lifecycle-journal
└─ worker-generation
   ├─ worker-control-liveness
   └─ route-session
      └─ pcm-relay-boundary
         └─ provisional-positive-drift-guard
```

## Modeling rule

A VF node exists when a behavior has a non-obvious ownership boundary, lifecycle rule, failure boundary, timing invariant, or evidence interpretation.

Do not create nodes merely because a C# file exists. Thin adapters, enums, DTOs, and straightforward wrappers stay as sources of a larger node.

Current examples:

- `endpoint-lifecycle` exists because notification order is deliberately non-authoritative and absence has distinct worker semantics.
- `worker-control-liveness` exists because a disposable worker must never be able to block supervisor progress through IPC or teardown.
- `pcm-relay-boundary` and its drift child exist because callback ordering, zero-fill, cushion semantics, and intentional trimming are not obvious from the class names.
- `lifecycle-journal` is intentionally small because it is evidence plumbing, not a second lifecycle state machine.

## Evidence discipline

Current nodes anchor facts to the implementation snapshot above. A later VF refresh should advance source revisions only when the current implementation is re-read.

`Legacy/V0.1/VF/` is different: its old revisions are historical evidence and must not be rewritten to match current understanding.

## Human drill-down

`round4-shell.fact.md` contains only cross-layer rationale that is awkward to encode as local claims. Detailed behavior belongs in the individual `.vf.md` nodes, not duplicated in the fact page.

When the VibeFactory compiler is available:

```text
vf-kb check VF
```
