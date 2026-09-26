# Public release checklist

This checklist is for the first formal public-facing V0.21 release.

The V0.21 Daily Test Candidate exists specifically so the runtime can be validated before this list is closed.

## Runtime validation

- [ ] Multi-day ordinary daily use completed
- [ ] Repeated endpoint disconnect/reconnect
- [ ] Repeated Windows sleep/resume
- [ ] Sleep while endpoint absent
- [ ] Endpoint reconnect after resume
- [ ] GameEffects / GameMedia / Media / Default switching
- [ ] Manual route restart after power cycles
- [ ] No repeated worker churn while endpoint absent
- [ ] No systematic retained-ring growth
- [ ] Provisional gradual trim has acceptable subjective behavior
- [ ] 20 ms hard recenter is rare enough for daily use
- [ ] Historical periodic micro-dropout state either not reproduced or documented

## Packaging

- [ ] Decide whether the first release is portable ZIP, installer, or both
- [ ] Produce Release build
- [ ] Embed formal V0.21 version metadata
- [ ] Add checksums
- [ ] Decide code-signing plan
- [ ] Verify clean-machine startup
- [ ] Verify uninstall/removal path if an installer is used

## Repository

- [ ] Create a **sanitized public repository/history** that excludes private VibeFactory implementation material
- [ ] Do **not** make the current private development repository public merely by changing repository visibility if private VibeFactory material exists anywhere in its Git history
- [ ] Verify public docs mention VibeFactory only at the approved high level: private, actively used in development, not required at runtime, implementation details undisclosed
- [ ] Choose and add an open-source LICENSE
- [ ] Add GitHub repository description
- [ ] Add topics such as `windows`, `bluetooth`, `le-audio`, `wasapi`, `audio`
- [ ] Add release assets
- [ ] Add issue templates
- [ ] Add a bug-report template with hardware/driver fields
- [ ] Add CI build workflow
- [ ] Add screenshot or short tray demo
- [ ] Confirm README links against the default branch

## Documentation

- [x] Public README structure
- [x] Chinese README entry
- [x] Getting started
- [x] Why this exists
- [x] How it works
- [x] Validation matrix
- [x] Troubleshooting
- [x] Project history
- [x] Architecture + ADRs
- [x] Contributing guide
- [ ] Finalize formal V0.21 release note
- [ ] Document packaged installation
- [ ] Add exact supported/tested hardware table after daily validation
- [ ] Add screenshots when the Tray UI is final

## Evidence and communication

- [ ] Prepare a concise reproduction report for the original Windows LE Audio failure
- [ ] Prepare one technical long-form article
- [ ] Prepare one short community post
- [ ] Prepare one Microsoft-focused issue/report with minimal speculation
- [ ] Link Microsoft public documentation for every platform-level claim
- [ ] Clearly label project observations versus Microsoft-documented behavior
- [ ] Publish lifecycle logs only after reviewing for machine-specific details

## Microsoft/community publishing paths

Potential channels:

- GitHub repository and Discussions
- Reddit / audio / Windows / Bluetooth communities
- Microsoft Q&A
- Microsoft Learn Tech Community
- Microsoft Learn Community Content where appropriate
- Feedback Hub / applicable Windows feedback channel
- relevant NAudio / Windows audio developer communities

Microsoft Learn contribution guidance:

- https://learn.microsoft.com/en-us/contribute/content/
- https://learn.microsoft.com/community/

A repository README can be the canonical technical source, while community articles explain the problem at different levels without duplicating implementation truth.

## Release rule

Do not silently rewrite a frozen candidate after finding a defect.

```text
candidate fails
→ preserve candidate
→ fix development branch
→ freeze next candidate if needed
```

Formal V0.21 should identify the exact validated baseline it was built from.
