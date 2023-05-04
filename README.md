<div align="center">
   <img src="assets/app.png" width="12.5%">
   <img src="assets/start.png" width="12.5%">
   <img src="assets/edit.png" width="12.5%">
   <img src="assets/stop.png" width="12.5%">
   <img src="assets/delete.png" width="12.5%">
   <img src="assets/continue.png" width="12.5%">
   <br>
   <br>
   <div>
      <a href="https://github.com/JamesNZL/flow-toggl-plugin/issues">
         <img src="https://img.shields.io/github/issues/jamesnzl/flow-toggl-plugin" alt="GitHub issues">
      </a>
      <a href="https://github.com/JamesNZL/flow-toggl-plugin/pulls">
         <img src="https://img.shields.io/github/issues-pr/jamesnzl/flow-toggl-plugin" alt="GitHub pull requests">
      </a>
      <a href="https://github.com/JamesNZL/flow-toggl-plugin/actions/workflows/release.yml">
         <img src="https://img.shields.io/github/actions/workflow/status/jamesnzl/flow-toggl-plugin/release.yml?branch=main" alt="GitHub workflow status">
      </a>
      <a href="https://github.com/JamesNZL/flow-toggl-plugin/commits">
         <img src="https://img.shields.io/github/last-commit/jamesnzl/flow-toggl-plugin" alt="GitHub last commit">
      </a>
   </div>
</div>

# Flow Toggl Plugin

A performant [Toggl Track](https://track.toggl.com/timer) plugin for [Flow Launcher](https://flowlauncher.com/) to bring time tracking right to your fingertips.

- [Features](#features)
- [Screenshots](#screenshots)
- [Setup Instructions](#setup-instructions)

# Features

- Simple, user-friendly interface
- Start new time entries
- Edit currently running time entries
- Stop currently running time entries
- Delete currently running time entries
- Continue a previously tracked time entry
- Open in browser shortcut
- Support for projects, clients, and workspaces
- Coloured icons for projects
- Human-friendly date and time display
- Open source
- Private and secure—all data is stored locally!

# Screenshots

## `tgl`
![Default hotkeys](./assets/screenshots/default.jpg)

## `tgl start`
![Project selection](./assets/screenshots/start.jpg)

![Start time options](./assets/screenshots/start-options.jpg)

## `tgl edit`
![Editing running time entry](./assets/screenshots/edit.jpg)

## `tgl stop`
![Stopping running time entry](./assets/screenshots/stop.jpg)

## `tgl delete`
![Deleting running time entry](./assets/screenshots/delete.jpg)

## `tgl continue`
![Continue previous time entry](./assets/screenshots/continue.jpg)

# Setup Instructions

1. Install the plugin.
    ```
    pm install Toggl Track
    ```

2. Paste your Toggl Track API key into the plugin settings.
    > This can be found at the bottom of your Toggl Track [profile settings](https://track.toggl.com/profile) page.

3. Trigger the plugin with the (configurable) action keyword `tgl`.
