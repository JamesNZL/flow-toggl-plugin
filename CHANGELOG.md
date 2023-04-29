# Changelog

All notable changes to this project will be documented in this file. See [standard-version](https://github.com/conventional-changelog/standard-version) for commit guidelines.

## [1.1.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v1.0.0...v1.1.0) (2023-04-29)


### Features

* **ux:** :lipstick: increase width of api token text box ([63e9387](https://github.com/JamesNZL/flow-toggl-plugin/commit/63e938754a8287ce7cd103575f7dc03c055d7686))

## 1.0.0 (2023-04-29)


### Features

* :tada: initial commit ([282b440](https://github.com/JamesNZL/flow-toggl-plugin/commit/282b4401a26060c520ff80b670cab8f762520f99))
* **toggl:** :sparkles: add toggl wrapper ([7df7c6e](https://github.com/JamesNZL/flow-toggl-plugin/commit/7df7c6ebbb141802228559d4738e25f9f94636aa))    
* **ux:** :children_crossing: add start/stop/continue icons ([a0581c1](https://github.com/JamesNZL/flow-toggl-plugin/commit/a0581c1f47b0649955817343cd0c1ef1c16d5278))
* **ux:** :children_crossing: enforce result scores ([157aea4](https://github.com/JamesNZL/flow-toggl-plugin/commit/157aea4caf2a0db2091ef37dd6d7e00f0d113bf0))
* **ux:** :children_crossing: filter archived projects and sort by total hours ([0a003bb](https://github.com/JamesNZL/flow-toggl-plugin/commit/0a003bb3e5e5ddf86a7d7a3550388f8aeff72e57))
* **ux:** :children_crossing: implement refresh command ([32c4ba3](https://github.com/JamesNZL/flow-toggl-plugin/commit/32c4ba3b1056ba34fb58d594eeb8146378c9198c))
* **ux:** :children_crossing: include client in project fuzzy search ([900eb9b](https://github.com/JamesNZL/flow-toggl-plugin/commit/900eb9b4bc92edba3a4f0fd69648f0e67b23d522))
* **ux:** :children_crossing: include project hours in selection list ([5773a19](https://github.com/JamesNZL/flow-toggl-plugin/commit/5773a19c14c9275cb519f5de0fb6d65b5828adf8))
* **ux:** :children_crossing: modify project selection autocomplete ([b126a02](https://github.com/JamesNZL/flow-toggl-plugin/commit/b126a0295239e54106343937a923f47959229027))
* **ux:** :passport_control: implement token verification ([945c8f0](https://github.com/JamesNZL/flow-toggl-plugin/commit/945c8f0180a323cb6dd35f7fb2dc17b53f019895))
* **ux:** :sparkles: add error result if token not configured ([6000c9d](https://github.com/JamesNZL/flow-toggl-plugin/commit/6000c9df66d7f3f3b36a2cf4082613fdf79f2ca2))
* **ux:** :sparkles: display project and client name when stopping entry ([ded4306](https://github.com/JamesNZL/flow-toggl-plugin/commit/ded430668a092c1c8bf85e1c933131c91e3c2201))
* **ux:** :sparkles: implement basic results menu ([a9c7966](https://github.com/JamesNZL/flow-toggl-plugin/commit/a9c79665199f336a22b4d5544baf8201c05e3184))
* **ux:** :sparkles: implement basic results menu ([b573110](https://github.com/JamesNZL/flow-toggl-plugin/commit/b5731101153658c8c0a73b6a8106152b55231063))
* **ux:** :sparkles: implement continue command ([db47a3f](https://github.com/JamesNZL/flow-toggl-plugin/commit/db47a3fcb8fd3266b977a6b69e2b7667be393225))
* **ux:** :sparkles: implement default hotkeys ([2307291](https://github.com/JamesNZL/flow-toggl-plugin/commit/23072913021a6c1feb7ac4c6bbe03439aa692505))
* **ux:** :sparkles: implement settings panel ([2b47e18](https://github.com/JamesNZL/flow-toggl-plugin/commit/2b47e185cf99754cda31f4c403db8a65c926a539))
* **ux:** :sparkles: implement start command ([65d64d0](https://github.com/JamesNZL/flow-toggl-plugin/commit/65d64d00148ce7c67acc49aeee6f605ad3ecba1d)) 
* **ux:** :sparkles: implement stop command ([45e5784](https://github.com/JamesNZL/flow-toggl-plugin/commit/45e57846dd67e5132d3d69375a598ac766213345))  
* **ux:** :sparkles: make call to stop time entry ([dd19732](https://github.com/JamesNZL/flow-toggl-plugin/commit/dd197325b2f0e0316574787f447a17587c282c76))
* **ux:** :sparkles: only show stop command if current timer ([99b56a2](https://github.com/JamesNZL/flow-toggl-plugin/commit/99b56a21774b7d7d9c54701c97fab6e6ee033ae5))
* **ux:** :sparkles: show coloured dot matching project colour ([b65f60f](https://github.com/JamesNZL/flow-toggl-plugin/commit/b65f60f3dafdf04e8b87e34cea2f755d3fe3edfa))
* **ux:** :speech_balloon: display dates and times in human-friendly format ([aadd110](https://github.com/JamesNZL/flow-toggl-plugin/commit/aadd110f4a35f3b359fc52b02f2badd9fab27771))


### Bug Fixes

* :bug: use `null`-safe access on `project` ([fa6272c](https://github.com/JamesNZL/flow-toggl-plugin/commit/fa6272c24d3a59a47facc3c05a8f10602f3714ca))  
* **toggl:** :bug: fix `BaseAddress` bug ([a55b871](https://github.com/JamesNZL/flow-toggl-plugin/commit/a55b871fc39a94850cf8085aef41462577af813a))     
* **toggl:** :bug: fix floating point api response types ([8348bef](https://github.com/JamesNZL/flow-toggl-plugin/commit/8348bef581dc63ad011566774d8e2308ff8161d7))
* **toggl:** :bug: use concrete classes for response types ([d6aebf0](https://github.com/JamesNZL/flow-toggl-plugin/commit/d6aebf0b7947d3837137d0adcfcc8653c38ce257))
* **ux:** :bug: fix elapsed duration calculation of current timer in continue panel ([39a76ff](https://github.com/JamesNZL/flow-toggl-plugin/commit/39a76ff96679a0450c78d622974fa444ec968556))