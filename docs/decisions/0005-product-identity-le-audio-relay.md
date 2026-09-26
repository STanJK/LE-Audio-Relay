# ADR 0005 — Product identity is LE Audio Relay

**Status:** Accepted  
**Date:** 2026-09-27

## Context

The project originated under the working identity **LE Audio Router** and repository name `le-audio-windows-relay`.

As the project moved toward public daily-test distribution, that identity became misleading. The application does not provide general-purpose arbitrary audio routing; its core behavior is a supervised Windows system-audio relay into a persistent Bluetooth LE Audio render endpoint.

## Decision

The canonical public product identity is:

```text
Product name       LE Audio Relay
Repository         STanJK/LE-Audio-Relay
Assembly           LEAudioRelay
Executable         LEAudioRelay.exe
Project file       LEAudioRelay.csproj
Root namespace     LEAudioRelay
Local app data     %LOCALAPPDATA%\LEAudioRelay\
```

Current configuration types also use `RelayConfiguration` and `RelayMode`.

## Historical boundaries

`Legacy/V0.1/` remains historical evidence and is not rewritten merely to match the current product identity.

Existing VibeFactory node IDs such as `leaudio-router.*` remain stable knowledge-base identifiers. Product renaming does not justify rewriting those IDs and breaking historical references.

## Runtime behavior

This decision is an identity migration only.

It does not intentionally change:

- the Process Loopback audio path;
- endpoint lifecycle reconciliation;
- worker generation semantics;
- suspend/resume behavior;
- timing or buffering policy;
- the provisional positive-drift guard.

## Consequence

New builds, documentation, logs, and public releases should use **LE Audio Relay / LEAudioRelay** consistently.

Any remaining use of **LE Audio Router / LEAudioRouter** outside frozen historical evidence should be treated as a stale reference.
