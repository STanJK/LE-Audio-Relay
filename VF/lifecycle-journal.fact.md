# Lifecycle Journal

## Why

The journal exists for reconstruction after long daily runs, not for real-time audio diagnostics.

## What

Persist only coarse lifecycle transitions under `%LOCALAPPDATA%/LEAudioRelay/logs`.

## Outcome

Failures can be correlated with reconnect, power, worker, and user actions without making logging part of the audio critical path.
