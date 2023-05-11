# Changelog

All notable changes to this project will be documented in this file. See [standard-version](https://github.com/conventional-changelog/standard-version) for commit guidelines.

## [1.8.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v1.7.0...v1.8.0) (2023-05-11)


### Features

* **ux:** :speech_balloon: add `"now"` to standard `start`/`stop` actions ([150e35c](https://github.com/JamesNZL/flow-toggl-plugin/commit/150e35c1c87f6e8132ea7c5d13517c99e256e67c))

## [1.7.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v1.6.1...v1.7.0) (2023-05-11)


### Features

* **ux:** :children_crossing: increase score of usage examples ([42c9c99](https://github.com/JamesNZL/flow-toggl-plugin/commit/42c9c9961308b4b44911ac81b80d313591db7647))
* **ux:** :sparkles: allow starting time entries in the past ([5f3886e](https://github.com/JamesNZL/flow-toggl-plugin/commit/5f3886e236d5606ac0808c8bb9cd6a53c5d9b9aa))
* **ux:** :sparkles: allow stopping time entries in the past ([1688230](https://github.com/JamesNZL/flow-toggl-plugin/commit/1688230545128d7921c5bf658dc0f38c67ab637e))
* **ux:** :sparkles: use `continue` to autofill `start` command ([6602bf8](https://github.com/JamesNZL/flow-toggl-plugin/commit/6602bf8c086cdb2bdf480973eef3a9a2bd8fa505))


### Bug Fixes

* **ux:** :bug: remove trailing space from final autocomplete ([e454a2f](https://github.com/JamesNZL/flow-toggl-plugin/commit/e454a2fe30ad2c24bbe2bb661baa0f4b09ef58bc))
* **ux:** :speech_balloon: fix started time entry message box titles ([a7f7757](https://github.com/JamesNZL/flow-toggl-plugin/commit/a7f775725f6153e2bbdcbdc56f34a328e9dba80d))

### [1.6.1](https://github.com/JamesNZL/flow-toggl-plugin/compare/v1.6.0...v1.6.1) (2023-05-04)


### Bug Fixes

* :bug: fix error with edit project colour icons ([6d4ca77](https://github.com/JamesNZL/flow-toggl-plugin/commit/6d4ca77ec4bf3343f776385747eaa085ff8633cd))

## [1.6.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v1.5.0...v1.6.0) (2023-05-04)


### Features

* **ux:** :children_crossing: tab autocomplete running entry description if empty edit ([098a52f](https://github.com/JamesNZL/flow-toggl-plugin/commit/098a52fe6033348f3cb851b9ae6cc0b6687bbe14))


### Bug Fixes

* :ambulance: fix `GDI+` exception ([13221b8](https://github.com/JamesNZL/flow-toggl-plugin/commit/13221b837a58aa0dae5857cd388d936a149aacc6))

## [1.5.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v1.4.0...v1.5.0) (2023-05-04)


### Features

* **ux:** :children_crossing: improve edited project selection and include project name in query ([1259161](https://github.com/JamesNZL/flow-toggl-plugin/commit/1259161233d068e156708fff54887c056717c48a))
* **ux:** :sparkles: allow starting new time entry at previous stop time ([095adbe](https://github.com/JamesNZL/flow-toggl-plugin/commit/095adbef9ec218cb3dbbcdbd9570d7d24a880244))
* **ux:** :sparkles: implement ability to edit current project ([d6b6dde](https://github.com/JamesNZL/flow-toggl-plugin/commit/d6b6dde854a21a1b498c3b5d8160ea55f8361e7b))

## [1.4.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v1.3.0...v1.4.0) (2023-05-02)


### Features

* **ux:** :lipstick: add refresh icon ([e96ecc3](https://github.com/JamesNZL/flow-toggl-plugin/commit/e96ecc396f76a6c0efffca4a103d0c8c03c98a3f))
* **ux:** :sparkles: implement open in browser command ([db61795](https://github.com/JamesNZL/flow-toggl-plugin/commit/db617953b0402c727fbc046c951b4fa5672e7c5f))

## [1.3.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v1.2.0...v1.3.0) (2023-04-30)


### Features

* **ux:** :sparkles: implement delete command ([02c4205](https://github.com/JamesNZL/flow-toggl-plugin/commit/02c42051cb7f8377199cf6d86e2d564920759644))

## [1.2.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v1.1.0...v1.2.0) (2023-04-30)


### Features

* :goal_net: handle network and response exceptions gracefully ([79e9393](https://github.com/JamesNZL/flow-toggl-plugin/commit/79e939303e43f6cd136d2fd39a21dc07610e6da1))
* **ux:** :goal_net: improve api error reporting ([e11c57e](https://github.com/JamesNZL/flow-toggl-plugin/commit/e11c57e08bb3111821f9d8ea2720e674d8049ac0))
* **ux:** :sparkles: implement edit command ([7460adb](https://github.com/JamesNZL/flow-toggl-plugin/commit/7460adbc18108487d6cdf0eb7ff13fc301e783cf))
* **ux:** :speech_balloon: correctly pluralise project hours ([76107dc](https://github.com/JamesNZL/flow-toggl-plugin/commit/76107dc020c514aa807bec0a066fb8061de985d1))


### Bug Fixes

* **ux:** :children_crossing: exclude project hours string from fuzzy search ([702b818](https://github.com/JamesNZL/flow-toggl-plugin/commit/702b81866e02c20db085af23b295060cf2e730f8))
* **ux:** :speech_balloon: do not display `client | hours` separator if no client ([5f0e61e](https://github.com/JamesNZL/flow-toggl-plugin/commit/5f0e61edd74ebd29c5ad36c48e4e0663a7949d8f))
* **ux:** :speech_balloon: fix no project display on continue results ([5383f92](https://github.com/JamesNZL/flow-toggl-plugin/commit/5383f923db925315df950827bee93f62ad6cc402))

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
