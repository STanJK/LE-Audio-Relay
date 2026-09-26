# Community and Microsoft publishing guide

This page is not runtime documentation.

It describes how to communicate LE Audio Relay and the underlying Windows LE Audio observations without mixing product promotion, platform facts, and unproven root-cause claims.

---

## Communication goals

The project has three distinct public goals.

### 1. Help users discover a practical tool

Message:

> Windows LE Audio can work well, but some users may hit lifecycle instability. LE Audio Relay is a focused user-mode workaround/experiment for keeping one destination render generation alive.

Best channels:

- GitHub README
- release posts
- Windows/audio communities
- short demo posts

### 2. Share engineering evidence

Message:

> Here is a reproducible sequence, the user-mode architecture used to isolate it, and the evidence we have collected across route rebuilds, endpoint reconnects, and power cycles.

Best channels:

- long-form technical article
- GitHub documentation
- developer communities
- NAudio / Windows audio discussions

### 3. Make the Windows problem actionable for Microsoft/OEM engineers

Message:

> Here is the smallest reproducible behavior we can describe, the exact system state transitions, what changes when the route stays alive, and what survives a fresh user-mode generation.

Best channels:

- Microsoft Q&A when appropriate
- Microsoft Learn Tech Community
- Feedback Hub / Windows feedback path
- relevant MicrosoftDocs issue/PR only when documentation itself needs correction
- direct engineering discussion if a team member engages

These three messages should not be collapsed into one giant post.

---

## Canonical source of truth

The GitHub repository should remain canonical for:

- current implementation;
- architecture decisions;
- validation status;
- reproduction notes;
- version history;
- logs and evidence format.

Community posts should link back to the relevant repository document instead of becoming a second source of implementation truth.

Recommended links:

- README → project overview
- WHY_THIS_EXISTS → problem/background
- HOW_IT_WORKS → technical architecture
- VALIDATION → evidence status
- TROUBLESHOOTING → user support
- PROJECT_HISTORY → evolution

---

## Claim discipline

Public writing should label statements by evidence type.

### Platform fact

Example:

> Windows 11 LE Audio support requires compatible platform hardware and manufacturer-provided LE Audio drivers.

Support with a Microsoft source.

### Project observation

Example:

> On our Galaxy Buds3 Pro test path, a reconstructed render route could sometimes return with an abnormal stereo image.

Use wording such as:

- "we observed";
- "on our test systems";
- "in this project's validation";
- "correlated with."

### Inference

Example:

> The behavior may involve state below the application AudioClient lifetime.

Use wording such as:

- "suggests";
- "is consistent with";
- "may indicate";
- "has not been proven."

Avoid:

- "Windows definitely corrupts phase";
- "Intel's driver is the cause";
- "Galaxy Buds firmware is broken";
- "Microsoft's LE Audio implementation always does X";

unless future evidence actually establishes those claims.

---

## Suggested community article

Possible title:

> **I kept Windows LE Audio alive for weeks — and ended up building a supervised audio relay**

Alternative:

> **Windows LE Audio is real, but lifecycle still matters: building a persistent WASAPI relay for Galaxy Buds3 Pro**

Suggested structure:

1. LE Audio on Windows is now real and useful.
2. Support is more platform/driver dependent than "Bluetooth 5.x".
3. The symptom: silence → route reconstruction → sometimes broken stereo presentation.
4. The key experiment: keep the final destination stream alive with zero PCM.
5. Why Process Loopback makes the workaround usable.
6. Why V0.1 was not enough for daily use.
7. Endpoint lifecycle: notification is wake-up, enumeration is truth.
8. Power lifecycle: pre-resume route generations are disposable.
9. Clock mismatch and the current temporary guard.
10. What remains unknown.
11. Invite hardware/driver/device reports.

The article should be readable without requiring readers to understand every internal class.

---

## Suggested short community post

Example shape:

```text
Windows LE Audio has become usable on more PCs, but I kept hitting a weird
lifecycle failure with Galaxy Buds3 Pro: after silence / route rebuild, stereo
presentation could come back wrong.

The workaround that stuck was surprisingly simple: never tear down the final
LE Audio render stream. Keep it alive and feed zero PCM during silence.

I turned that into a small Windows tray relay using Process Loopback, automatic
endpoint reconnect, sleep/resume generation replacement, and lifecycle logs.

Current status: daily-test candidate, not stable yet.

Repo:
<repo link>

Technical background:
<WHY_THIS_EXISTS link>
```

Then adapt tone for each community.

---

## Suggested Microsoft-focused report

Do not lead with the application.

Lead with the behavior.

### Title shape

> LE Audio render-route reconstruction can return Galaxy Buds3 Pro in inconsistent stereo state after silence

### Environment

```text
Windows build:
PC / platform:
Bluetooth controller:
Bluetooth driver:
Audio driver:
Endpoint:
Endpoint firmware:
"Use LE Audio when available": present / enabled
```

### Minimal reproduction

Describe the shortest path that reproduces the bad state without the router if possible.

Example structure:

```text
1. Connect LE Audio endpoint.
2. Play a known stereo/mono test signal.
3. Allow the render path to become inactive after silence.
4. Resume playback.
5. Repeat until the abnormal state appears.
```

Then describe the observable symptom without over-interpreting it.

### Control experiment

```text
Keep one shared-mode render stream continuously active.
Feed zero PCM during application silence.
Repeat the same workload.
```

Report whether the failure reproduces.

This control experiment is more useful to an engineer than saying "my custom app fixes it."

### Lifecycle evidence

Include:

- endpoint add/remove/state timing;
- whether a fresh user-mode worker fixes it;
- whether reconnect fixes it;
- whether Windows reboot fixes it;
- whether power-cycle history matters.

### Root-cause statement

Recommended:

> The project does not currently identify the responsible layer. The result only shows that preserving the final render generation materially changes reproducibility on the tested stack.

---

## Microsoft Learn

Microsoft Learn explicitly supports community contribution workflows, including documentation contributions, Microsoft Q&A, Tech Community participation, and community content.

Useful entry points:

- https://learn.microsoft.com/en-us/contribute/content/
- https://learn.microsoft.com/community/

However, not every project-specific article belongs directly in a Microsoft product documentation repository.

A reasonable sequence is:

1. publish the repository and technical evidence;
2. publish a neutral community technical article;
3. use Microsoft Q&A / Tech Community / Feedback channels to surface the reproducible Windows behavior;
4. contribute to Microsoft Learn documentation only where the project reveals a genuine documentation gap or where the community-content path accepts the article scope.

A Microsoft-facing article should teach Windows/LE Audio behavior first and mention LE Audio Relay as the reproducible experiment/tool, not read as product advertising.

---

## Good topics for a Microsoft Learn-style article

Potential educational angle:

### "Debugging Bluetooth LE Audio capability on Windows 11"

Could cover:

- why Bluetooth LE support is not equivalent to LE Audio support;
- the **Use LE Audio when available** check;
- Bluetooth/audio driver dependency;
- endpoint lifecycle;
- how to collect controller/driver/build information;
- distinction between Bluetooth Classic and LE Audio;
- how Process Loopback can be used for controlled audio experiments.

LE Audio Relay can appear as a case study rather than the entire article.

### "Building a Process Loopback audio relay on Windows"

Could cover:

- IncludeTargetProcessTree vs ExcludeTargetProcessTree;
- why process exclusion prevents recursive capture;
- shared-mode render lifetime;
- lifecycle ownership;
- safe callback design;
- endpoint notification vs authoritative enumeration.

This is more broadly useful than a device-specific bug report.

---

## Tone

Preferred public tone:

- technical;
- reproducible;
- curious;
- precise about uncertainty;
- respectful toward Microsoft, OEMs, driver vendors, and device vendors.

The project becomes more useful to platform engineers when it reads like an evidence package rather than a complaint.

---

## Publication readiness

Before a broad launch:

- finish the V0.21 daily validation round;
- choose a license;
- publish a clean Release build;
- add at least one screenshot or short demo;
- freeze the exact public baseline;
- ensure README/Validation reflect only tested claims;
- prepare one minimal Microsoft reproduction report;
- prepare one longer explanatory article.

See [Public release checklist](PUBLIC_RELEASE_CHECKLIST.md).
