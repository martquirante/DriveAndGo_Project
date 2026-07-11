# DEPRECATED & ARCHIVED: DriveAndGo_App (.NET MAUI)

> [!WARNING]
> This legacy .NET MAUI mobile app has been **DECOMMISSIONED & DISABLED** as part of the system refactoring.

## Reason for Migration
The mobile client frontend is being replaced with a modern **Flutter cross-platform application**.

## Backend Integration
All client apps (WinForms Admin, SuperAdmin, and the new Flutter App) communicate strictly through the centralized REST API server (`DriveAndGo_API`) via HTTP REST endpoints (`/api/...`) with JWT Bearer authentication.

Direct database connections from client apps have been completely removed.
