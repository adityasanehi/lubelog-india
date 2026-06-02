# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Run locally (hot-reload)
dotnet watch run

# Build
dotnet build

# Publish release build
dotnet publish -c Release -o out

# Docker (production)
docker compose up --build

# Docker with PostgreSQL
docker compose -f docker-compose.postgresql.yml up --build
```

There is no test project in this repository.

Set `LUBELOGGER_LOCALE_OVERRIDE=hi-IN` (or any .NET culture string) as an environment variable or in `appsettings.Development.json` to override locale and currency symbol globally. Setting it to `hi-IN` automatically yields `₹` via `CultureInfo`.

## Architecture

### Data access — dual-backend repository pattern

Every entity has:
1. An interface in `External/Interfaces/` (e.g. `IReminderRecordDataAccess`)
2. Two concrete implementations: `External/Implementations/Litedb/` and `External/Implementations/Postgres/`

`Program.cs` registers either the LiteDB or Postgres set at startup based on whether `POSTGRES_CONNECTION` is set. **When adding a new record type, you must implement both backends and register both in `Program.cs`.**

LiteDB is always injected even when Postgres is selected (used for migration and file-based operations via `ILiteDBHelper`).

### Configuration — two tiers

- **Server config** (`ServerConfig`): system-wide settings, read from env vars prefixed `LUBELOGGER_*` or `data/config/serverConfig.json`. Accessed via `IConfigHelper`. Examples: auth, SMTP, webhooks, locale override.
- **User config** (`UserConfig`): per-user preferences stored in the DB. Accessed via `IUserConfigDataAccess`. Examples: dark mode, MPG preference, visible tabs, language.

`UserConfig.UserLanguage` drives translation lookups. `LUBELOGGER_LOCALE_OVERRIDE` drives `CultureInfo` (and thus currency symbol rendering).

### Currency symbol

Currency is not hardcoded. In `Views/Shared/_Layout.cshtml`, the symbol and position come from `CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol` and `CurrencyPositivePattern`. To change currency symbol, change the server locale. No code changes needed for ₹ — just set culture to `hi-IN`.

### Translation / i18n

`TranslationHelper` loads JSON dictionaries from:
- `wwwroot/defaults/en_US.json` — canonical English strings (source of truth, keys are words with spaces replaced by `_`)
- `data/translations/{locale}.json` — override files for other locales, merged on top of `en_US`

The `ITranslationHelper.Translate(userLanguage, text)` method is called from controllers passing the user's language. Views receive translated strings via ViewModels, not by calling the helper directly.

To add a new locale: create `data/translations/hi_IN.json` with overrides. The admin UI supports uploading translation files.

### Reminders system (already exists)

`ReminderRecord` supports date-based, odometer-based, or both metrics. Recurrence is fully supported via `ReminderMonthInterval`, `ReminderMileageInterval`, and custom intervals. `ReminderHelper.GetReminderRecordViewModels` computes urgency dynamically. This is the foundation to build Insurance and Pollution Check reminders on — use it rather than creating new reminder types.

### Notifications pipeline

`NotificationLogic` handles automated reminder dispatch:
- **Email** via `MailHelper` (MailKit/SMTP)
- **Webhooks** via `NotificationServiceConfig` (configurable HTTP POST with templated body — supports Ntfy, Gotify, Pushover, etc.)
- **SignalR** (in-app realtime) via `EventLogic` → `EventHubLogic`

There is no Web Push (VAPID) implementation. Webhook-based push is the current extensibility point. `AutomatedEventLogic` is an `IHostedService` that triggers `NotificationLogic.RunAutomatedEvents()` on a schedule configured by `NotificationConfig.HourToCheck` / `MinuteToCheck`.

### Tabs / feature visibility

The `ImportMode` enum in `Models/` controls which tabs appear. Each tab is a vehicle record type. `UserConfig.VisibleTabs` and `TabOrder` control visibility and ordering per user. Adding a new tab requires adding a value to `ImportMode` and wiring it in the settings UI.

### Frontend

Vanilla JS per-feature files in `wwwroot/js/` (no bundler, no framework). Bootstrap 5 + jQuery. `wwwroot/js/shared.js` contains cross-cutting utilities used by all pages. Chart.js for reports. SignalR client for realtime events.

Views are Razor `.cshtml` files under `Views/`. Partials live in `Views/Shared/`. The layout (`_Layout.cshtml`) injects locale, currency symbol, and SignalR connection into every page via inline `<script>` blocks.

### PWA / manifest

`wwwroot/manifest.json` and icon assets already exist. Service worker and VAPID Web Push are not yet implemented.

### India-specific fork context

This fork (`adityasanehi/lubelog-india`) targets Indian users. The upstream project is `hargata/lubelog`. India-specific additions should be:
- **Additive** where possible (feature flags, new tabs, new reminder tags) to stay mergeable with upstream
- **Locale-driven** for currency (set `hi-IN` culture, no hardcoded symbols)
- New record types (Insurance, Pollution/PUC) should follow the existing `ReminderRecord` pattern rather than bespoke schemas when only date/expiry tracking is needed
