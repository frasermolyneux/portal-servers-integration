---
description: "Concise guidance on when to emit audit events versus metrics and logs in portal runtime code."
applyTo: "src/**/*.cs"
---

# Auditing Balance Guidance

Use audit events for high-value, externally meaningful actions. Prefer metrics and warning/error logs for high-frequency operational telemetry.

## Emit audit events when

- A persistent state change occurs (create, update, delete, import, moderation outcome).
- A security, authorization, or privileged action occurs.
- A user or service action has compliance or forensic value.
- An externally consequential action succeeds or fails and needs a durable trail.

## Avoid audit events when

- The signal is a periodic heartbeat or status snapshot.
- The signal is a repeated loop checkpoint or decision gate.
- The signal is a high-frequency "sent", "matched", "skipped", or "received" operational event with no state change.
- The same trend is better represented by metrics (counts, rates, durations) plus warning/error logs.

## Exception and warning observability

- Do not rely on custom audit events for failure visibility.
- Keep exceptions and warning/error logs as the primary failure signal.
- If removing low-value audit events, verify failure paths still emit exceptions and warning/error logs.

## Change gate for new audit events

- Before adding a new audit event, document why metrics or existing logs are insufficient.
- Include expected event volume and retention value in the PR description.
