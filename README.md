# XtremeIdiots Portal - Servers Integration
[![Build and Test](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/build-and-test.yml)
[![Code Quality](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/codequality.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/codequality.yml)
[![Copilot Setup Steps](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/copilot-setup-steps.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/copilot-setup-steps.yml)
[![Dependabot Auto-Merge](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/dependabot-automerge.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/dependabot-automerge.yml)
[![Deploy Dev](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-dev.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-dev.yml)
[![Deploy Prd](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-prd.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/deploy-prd.yml)
[![Destroy Development](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-development.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-development.yml)
[![Destroy Environment](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-environment.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/destroy-environment.yml)
[![PR Verify](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/pr-verify.yml)
[![Release - Publish NuGet](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-publish-nuget.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-publish-nuget.yml)
[![Release - Version and Tag](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-version-and-tag.yml/badge.svg)](https://github.com/frasermolyneux/portal-servers-integration/actions/workflows/release-version-and-tag.yml)

## Documentation
* [Development Workflows](/docs/development-workflows.md) - Branch strategy, CI/CD triggers, NuGet publishing, and development flows
* [Manual Steps](/docs/manual-steps.md) - Post-deployment configuration steps
* [Platform Settings Contracts](/docs/platform-settings-contracts.md) - Resolver architecture, migration notes, and troubleshooting runbook
* [Testing](/docs/testing.md) - Test strategy and package testing helpers for API client consumers

## Overview
Versioned REST API that bridges the XtremeIdiots Portal with game servers: querying live status, running RCON operations, and syncing maps over FTP or SFTP. Uses ASP.NET Core 9 with Microsoft.Identity.Web for Entra-protected controllers, Application Insights telemetry with sampling, optional Azure App Configuration, and a generated Repository API client for portal data. Packages abstractions and API clients for reuse across the ecosystem and deploys to Azure App Service via Terraform with OIDC authentication.

The service-account-only CoD4x `plugins/portal-health` endpoint returns the raw
`portalpluginhealth` RCON response. Unsupported or absent plugin health output
is not evidence of installed capabilities; confirm the actual response at runtime.

## RCON messages

Message requests accept the existing `message` and an optional ordered `messages`
list. When both are supplied, `message` is sent first. Each rendered item is
validated and split before dispatch, preserving item order and CoD colour continuation.
Source game messages treat colour-like sequences as literal text.
Generic message requests also accept optional `firstFragmentHeader`, including any
desired trailing separator. It is validated using the channel's character rules
and kept together on only the first fragment of each item, within the same byte
budget as the body and colour continuation. Omitted or null headers preserve
existing behavior; already-frozen rules fragments are not re-prepared.
All fragments are validated before the first send. A partial failure returns an
error containing completed and total fragment counts; its final attempted fragment
may have an unknown outcome. Successful or partially dispatched batches are audited
and reported as local dispatch, not confirmed player delivery. Messaging commands
are not automatically retried, and UDP cannot guarantee exactly-once or ordered
delivery.

The interim RCON policy allows 149 total display bytes, including prefix and colour
overhead. CoD4x chat's client buffer is source-verified; other game and
screen/console cutoffs and the 32-byte prefix allowance are not independently
verified. Direct CoD4x chat reads the current prefix to prepare and send the first
fragment, then checks it again before each later fragment. External prefix changes
between a check and a send cannot be detected atomically, and localization can
expand the prefix beyond its raw length. RCON accepts printable ASCII excluding
quotes, backslashes and semicolons; CoD4x also rejects localization inserts of the
form `&&` followed by a digit. Unsupported text fails rather than being altered.

CoD4x bot-owned chat headers require the server operator to configure
`sv_consayname ""` and `sv_contellname ""` in the actual startup configuration
after identifying its execution order and overrides. These server-wide settings
remove automatic headers from all RCON/console `say` and `tell`, not just bots;
private screen/console messages retain their engine-added PM prefix. The API
neither changes these settings nor assumes a configuration file path. Verify
the live values after the configuration is loaded. Release the shared splitter
package, deploy this API and publish its client contracts before upgrading
header-aware producers; older API deployments may ignore the new request field.

CoD4x client-kick, only-kick and ban-client requests must include `clientId`;
slot zero is valid. Structured ban endpoints currently accept only numeric
player identifiers of 17–21 digits, not every identifier format returned by status.
Temporary bans send the requested duration in minutes to the game server; server
versions may impose their own duration limits. Structured ban actions emit an
audit and operator event only for a confirmed native success response. Check
the response outcome; an HTTP 200 alone does not establish a successful action.

## Redirect-backed map transfers

The managed identity requires `Maps.Read`, with `RedirectApi:BaseUrl` and
`RedirectApi:ApplicationAudience` in this application's App Configuration
namespace. Transfers load the current redirect manifest and use the shared
authenticated streaming client, not repository-provided download URLs.

Explicit pushes refresh selected `.ff` and `.iwd` files, including nested files
and existing destination folders. Verification checks filenames and byte sizes,
not just folder presence or content hashes. Failed or incomplete transfers
return an error; retrying a push retransfers its selected files. Resumable
browser uploads/downloads and ZIP collections belong to the redirect manager,
not a game-server transfer journal.

## RCON roster observations

CoD2, CoD4, CoD5 and CoD4x status responses include an optional `roster` observation.
Recognized status output is `Partial`: RCON has no reliable response terminator or
player count, so even an empty roster cannot establish absence. Invalid responses
are `Failed`; transport failures use the existing API error response. Missing
`roster` on older servers means unknown, not complete. Partial observations expose
parsed identifiers and counts of unresolved rows, missing identifiers and explicit
bot-address rows. Identifiers are not proof of human players; bots without an
explicit marker cannot be distinguished. The observation contains no addresses or
raw responses; legacy status fields are unchanged.

## Rules native actions

The service-account-only `v1/rcon/{serverId}/rules-action` accepts one shared
`RuleActionCommand` and returns a correlated `RuleActionResult`. CoD2, CoD4,
CoD5 and CoD4x can dispatch kicks and public/private messages when configured
for RCON; Source games are unsupported by this adapter. A configured kick reason
is retained in audit/event data but is not sent to the game server because these
RCON adapters issue slot-only kicks. Messages that cannot be represented safely
by the ASCII RCON transport are skipped, not altered. Bans and expiry remain
cloud/agent responsibilities.

The RCON adapter accepts only agent-observed identities, verifies the current
fresh repository binding, then rechecks the slot and identifier immediately
before dispatch. Private rule messages may specify a separately verified recipient
without changing the original trigger identity; moderation remains trigger-bound.
The RCON-only `rules-action/message-budget` lookup reports channel availability,
prefix reservation and character constraints. It does not choose between RCON and
plugin providers. Plugin-owned identities must use the plugin executor. An
incomplete roster cannot establish absence, and RCON cannot verify a native
connection generation or eliminate slot-reuse races. UDP dispatch is not
confirmation: `Dispatched` and `OutcomeUnknown` must never be blindly retried
or treated as `Applied`. Dry runs send nothing.
Successful dispatches emit `NativeAction.{effect}.Rcon.Dispatched` events with
a deterministic per-effect idempotency key. Event write failures are logged and
do not cause the native command to be sent again.

## NuGet Packages

| Package                                                                                                                                                     | Latest                                                                                                                                                                                                  | Description                                               |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------- |
| [`XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1`](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1)       | [![NuGet](https://img.shields.io/nuget/v/XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1.svg)](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Abstractions.V1/)       | Shared contracts and DTOs for the Servers Integration API |
| [`XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1`](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1)           | [![NuGet](https://img.shields.io/nuget/v/XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1.svg)](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1/)           | Typed client for Servers Integration API V1               |
| [`XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing`](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing) | [![NuGet](https://img.shields.io/nuget/v/XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.svg)](https://www.nuget.org/packages/XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing/) | In-memory fakes and factory helpers for consumer tests    |

The Abstractions package requires Settings.Contracts.V1 5.x. Consumers using
legacy welcome-message types removed in 5.x must migrate those usages when upgrading.

## Contributing
Please read the [contributing](CONTRIBUTING.md) guidance; this is a learning and development project.

## Security
Please read the [security](SECURITY.md) guidance; I am always open to security feedback through email or opening an issue.
