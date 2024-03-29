# Changelog

All notable changes to this project will be documented in this file. See [standard-version](https://github.com/conventional-changelog/standard-version) for commit guidelines.

## [4.1.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v4.0.0...v4.1.0) (2024-02-02)


### Features

* **ux:** :sparkles: highlight interpolated time entry description ([#117](https://github.com/JamesNZL/flow-toggl-plugin/issues/117)) ([10f4289](https://github.com/JamesNZL/flow-toggl-plugin/commit/10f4289c59d25f6f4ec38bc56c5dbeead5ce3eba))

## [4.0.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v4.0.0-2...v4.0.0) (2023-07-09)

## [4.0.0-2](https://github.com/JamesNZL/flow-toggl-plugin/compare/v4.0.0-1...v4.0.0-2) (2023-07-08)


### Features

* **ux:** :children_crossing: hide `start` and `continue` results if searching for command ([#112](https://github.com/JamesNZL/flow-toggl-plugin/issues/112)) ([00aa5d6](https://github.com/JamesNZL/flow-toggl-plugin/commit/00aa5d609ac70f388eebcf9466279d1f2308d303))

## [4.0.0-1](https://github.com/JamesNZL/flow-toggl-plugin/compare/v4.0.0-0...v4.0.0-1) (2023-07-07)


### Features

* **reports:** :sparkles: implement reports of any arbitrary date/span ([#55](https://github.com/JamesNZL/flow-toggl-plugin/issues/55)) ([e45e46e](https://github.com/JamesNZL/flow-toggl-plugin/commit/e45e46ed36e9e5a58eb7950fd0439c56d6f1388d))
* **ux:** :children_crossing: add trailing space after project selection ([#105](https://github.com/JamesNZL/flow-toggl-plugin/issues/105)) ([b4208c8](https://github.com/JamesNZL/flow-toggl-plugin/commit/b4208c89059550f1ae50552dc16726e3b357f6fe))
* **ux:** :children_crossing: implement `-C` clear flag for `start` and `continue` ([#97](https://github.com/JamesNZL/flow-toggl-plugin/issues/97)) ([7fd55fc](https://github.com/JamesNZL/flow-toggl-plugin/commit/7fd55fc7d6675f71b971313079aa5d900707563e))
* **ux:** :children_crossing: implement `-l` list flag for `continue` ([#98](https://github.com/JamesNZL/flow-toggl-plugin/issues/98)) ([1f57cd9](https://github.com/JamesNZL/flow-toggl-plugin/commit/1f57cd904e44b46b77a7dfb299c14873e26d07c3))
* **ux:** :lipstick: use error icon for all errors ([#103](https://github.com/JamesNZL/flow-toggl-plugin/issues/103)) ([4d16b20](https://github.com/JamesNZL/flow-toggl-plugin/commit/4d16b2090e93f2fb9f2c5e5d717b6b47b26a7d72))
* **ux:** :sparkles: implement `-R` resume flag for `edit` ([#107](https://github.com/JamesNZL/flow-toggl-plugin/issues/107)) ([a621035](https://github.com/JamesNZL/flow-toggl-plugin/commit/a6210353a6df362aa7b6674b36abfeaa282793b0))


### Bug Fixes

* :bug: fix created description of `\` when quick-starting empty time entry ([#106](https://github.com/JamesNZL/flow-toggl-plugin/issues/106)) ([a761e63](https://github.com/JamesNZL/flow-toggl-plugin/commit/a761e63135222fd824466303ec713a9f208e73a1))
* **ux:** :bug: fix `edit` erroneously resuming time entry when using `-t` ([#104](https://github.com/JamesNZL/flow-toggl-plugin/issues/104)) ([1efca81](https://github.com/JamesNZL/flow-toggl-plugin/commit/1efca81c084f5121c4aaa47a6004738229391843))
* **ux:** :children_crossing: fix missing `edit` usage tips if any flag present ([#108](https://github.com/JamesNZL/flow-toggl-plugin/issues/108)) ([4ddf83c](https://github.com/JamesNZL/flow-toggl-plugin/commit/4ddf83cdfcb8977f0f8451bee2ca0a6d31f1db71))

## [4.0.0-0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v3.0.1...v4.0.0-0) (2023-07-04)


### ⚠ BREAKING CHANGES

* **ux:** See release notes/below.
1. `tgl start` and `tgl continue` command keywords have been removed.
2. `start` and `continue` are now top-level commands.
4. You can no longer quick-start commands with eg `tgl o`, you must now type the command in full (or select it from the top-level results)
5. `start` project selection is no longer enforced at the beginning; it is optionally triggered at any point with the `@` symbol.
6. `edit` project selection uses `@` instead of `-p` accordingly.

### Features

* **ux:** :children_crossing: display currently running timer at top level ([#91](https://github.com/JamesNZL/flow-toggl-plugin/issues/91)) ([aacbd2f](https://github.com/JamesNZL/flow-toggl-plugin/commit/aacbd2f43f7b47f5527a4b322d90da88afa62f15))
* **ux:** :sparkles: implement new command flow ([#28](https://github.com/JamesNZL/flow-toggl-plugin/issues/28)) ([#90](https://github.com/JamesNZL/flow-toggl-plugin/issues/90)) ([92d2cea](https://github.com/JamesNZL/flow-toggl-plugin/commit/92d2ceae7b6c55259c49713fae0c84672d196d9f))

### [3.0.1](https://github.com/JamesNZL/flow-toggl-plugin/compare/v3.0.0...v3.0.1) (2023-07-03)


### Performance

* :zap: implement `CancellationToken` properly ([#87](https://github.com/JamesNZL/flow-toggl-plugin/issues/87)) ([9c88afa](https://github.com/JamesNZL/flow-toggl-plugin/commit/9c88afa23da5051715a8608d0bbe225c8732a130))

## [3.0.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.10.1...v3.0.0) (2023-07-02)


### ⚠ BREAKING CHANGES

* **ux:** `-t` no longer works to specify the stop time with `tgl stop`, as this is the flag that is used to specify the start time with all other commands.

Instead, `-T` will take its place to specify the stop time to match the other commands.

### Features

* :technologist: create `TransformedQuery` extension class ([#82](https://github.com/JamesNZL/flow-toggl-plugin/issues/82)) ([0cab081](https://github.com/JamesNZL/flow-toggl-plugin/commit/0cab081db194f024317c72990cb8be97e717153a))
* **readme:** :memo: add command reference to `README` ([#60](https://github.com/JamesNZL/flow-toggl-plugin/issues/60)) ([9df1397](https://github.com/JamesNZL/flow-toggl-plugin/commit/9df1397857d7a44e0ff5958a4399503bacbca54a))
* **reports:** :children_crossing: change `-E` show stop time flag to more sensible `-S` ([#81](https://github.com/JamesNZL/flow-toggl-plugin/issues/81)) ([c188242](https://github.com/JamesNZL/flow-toggl-plugin/commit/c1882423254168f5cee5b02443c61bd30654d48f))
* **reports:** :sparkles: add `-E` to show time entry stop times ([#81](https://github.com/JamesNZL/flow-toggl-plugin/issues/81)) ([349f152](https://github.com/JamesNZL/flow-toggl-plugin/commit/349f15227d084cf44208fe669b93e434b76dd93f))
* **reports:** :sparkles: display totals for fuzzy filtered reports ([#64](https://github.com/JamesNZL/flow-toggl-plugin/issues/64)) ([7568f2a](https://github.com/JamesNZL/flow-toggl-plugin/commit/7568f2a6bb6eab5927e1af0bcfb650b18da7e6d1))
* **ux:** :boom: use `-T` flag to change `tgl stop` end time ([#58](https://github.com/JamesNZL/flow-toggl-plugin/issues/58)) ([2363573](https://github.com/JamesNZL/flow-toggl-plugin/commit/2363573837a7c2f35e06b7dd8bd0b0dd4186eaa6))
* **ux:** :children_crossing: add `tgl edit` flag to clear description ([#66](https://github.com/JamesNZL/flow-toggl-plugin/issues/66)) ([d499a9b](https://github.com/JamesNZL/flow-toggl-plugin/commit/d499a9bcb10909afa11ec0c53dc4fc5c37e1e35a))
* **ux:** :children_crossing: implement `continue`-like autofill for `tgl edit` ([#56](https://github.com/JamesNZL/flow-toggl-plugin/issues/56)) ([74f58a2](https://github.com/JamesNZL/flow-toggl-plugin/commit/74f58a2ade03d13c0db98bd0268f0d93710a986c)), closes [#28](https://github.com/JamesNZL/flow-toggl-plugin/issues/28)
* **ux:** :lipstick: remove `start`/`stop` `now` options when time span is specified ([#68](https://github.com/JamesNZL/flow-toggl-plugin/issues/68)) ([6a742d7](https://github.com/JamesNZL/flow-toggl-plugin/commit/6a742d741d6161886d530f07ef9839f8ec041639))
* **ux:** :sparkles: add `tgl help` command ([#65](https://github.com/JamesNZL/flow-toggl-plugin/issues/65)) ([2101067](https://github.com/JamesNZL/flow-toggl-plugin/commit/210106757c2d55a5a50c70693a615813c5f50c93))
* **ux:** :sparkles: implement flag escaping ([#67](https://github.com/JamesNZL/flow-toggl-plugin/issues/67)) ([8a657d3](https://github.com/JamesNZL/flow-toggl-plugin/commit/8a657d36c003bcf702e1b8bb1c4211e9b2aa9713))


### Bug Fixes

* **structures:** :bug: fix default trailing space behaviour ([#62](https://github.com/JamesNZL/flow-toggl-plugin/issues/62), [#63](https://github.com/JamesNZL/flow-toggl-plugin/issues/63)) ([97b4b8a](https://github.com/JamesNZL/flow-toggl-plugin/commit/97b4b8a568d4dcaa62365f6178e42fd30755a017))
* **ux:** :bug: fix broken fuzzy filter on detailed `tgl reports [span] projects` ([#78](https://github.com/JamesNZL/flow-toggl-plugin/issues/78)) ([9bfdb37](https://github.com/JamesNZL/flow-toggl-plugin/commit/9bfdb377e2eee3c219290312986ad17dc665e0cb))
* **ux:** :bug: fix buggy `tgl reports span-[offset]` detection ([#79](https://github.com/JamesNZL/flow-toggl-plugin/issues/79)) ([6425713](https://github.com/JamesNZL/flow-toggl-plugin/commit/6425713de50c1b52884dd369596f7b3e9ec41a43))
* **ux:** :bug: fix fuzzy filter of 'static' results ([#75](https://github.com/JamesNZL/flow-toggl-plugin/issues/75)) ([e1a4970](https://github.com/JamesNZL/flow-toggl-plugin/commit/e1a497009bfe91e41ece9fc242b806df45f30977))
* **ux:** :bug: fix inverse ordering of `tgl edit` projects ([#74](https://github.com/JamesNZL/flow-toggl-plugin/issues/74)) ([03f2c78](https://github.com/JamesNZL/flow-toggl-plugin/commit/03f2c7802a2ed8d328e4d6966002a259527f7385))
* **ux:** :bug: fix non-reversible `\` escaping ([#69](https://github.com/JamesNZL/flow-toggl-plugin/issues/69)) ([3f5a679](https://github.com/JamesNZL/flow-toggl-plugin/commit/3f5a679b6af2064d52a09b058f56d1c5b6affef5))
* **ux:** :bug: fix result score overflow ([#71](https://github.com/JamesNZL/flow-toggl-plugin/issues/71)) ([1cc9119](https://github.com/JamesNZL/flow-toggl-plugin/commit/1cc91194ed47967a8d88d8d27b9f239e6fa4a33e))
* **ux:** :bug: use `string.IsNullOrEmpty` everywhere ([#76](https://github.com/JamesNZL/flow-toggl-plugin/issues/76)) ([8909d97](https://github.com/JamesNZL/flow-toggl-plugin/commit/8909d9711d9d17d38795455dd76af8e6c98e185e))

### [2.10.1](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.10.0...v2.10.1) (2023-06-15)


### Bug Fixes

* **ux:** :bug: fix autocomplete with too many trailing spaces ([#59](https://github.com/JamesNZL/flow-toggl-plugin/issues/59)) ([a5610c9](https://github.com/JamesNZL/flow-toggl-plugin/commit/a5610c963fbd4ee7d688a5c5457da27eda91c4d8))

## [2.10.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.9.0...v2.10.0) (2023-06-14)


### Features

* **ux:** :children_crossing: do not clear `tgl edit` description by default ([#57](https://github.com/JamesNZL/flow-toggl-plugin/issues/57)) ([db5485c](https://github.com/JamesNZL/flow-toggl-plugin/commit/db5485cbe19e46e3aaf28fa5bf5429aaf1fa8906))

## [2.9.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.8.0...v2.9.0) (2023-06-10)


### Features

* **ux:** :children_crossing: autocomplete command on whitespace ([#52](https://github.com/JamesNZL/flow-toggl-plugin/issues/52)) ([dcf4161](https://github.com/JamesNZL/flow-toggl-plugin/commit/dcf416161121a8ef027561b11e31b2b23ec7f41d))

## [2.8.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.7.0...v2.8.0) (2023-06-07)


### Features

* **ux:** :zap: improve loading time of commands when cache is empty ([#49](https://github.com/JamesNZL/flow-toggl-plugin/issues/49)) ([ed00bda](https://github.com/JamesNZL/flow-toggl-plugin/commit/ed00bda91b54117ad2806d699188e05cc517c700))


### Bug Fixes

* **toggl:** :zap: fix erroneous forced fetch of `Me` ([#50](https://github.com/JamesNZL/flow-toggl-plugin/issues/50)) ([eee7569](https://github.com/JamesNZL/flow-toggl-plugin/commit/eee75695e367405ab1f260c8cfc7f6e7351d159f))

## [2.7.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.6.0...v2.7.0) (2023-06-05)


### Features

* **ux:** :sparkles: implement editing of stop time ([#46](https://github.com/JamesNZL/flow-toggl-plugin/issues/46)) ([acc2e2f](https://github.com/JamesNZL/flow-toggl-plugin/commit/acc2e2f9f39ae1cf94608f0feec9718fffcb5d2c))


### Bug Fixes

* **reports:** :bug: fix wrongful inclusion of running time entry in detailed project report ([#47](https://github.com/JamesNZL/flow-toggl-plugin/issues/47)) ([fecff39](https://github.com/JamesNZL/flow-toggl-plugin/commit/fecff390442e465a4e817dac8efbaeb6aa447225))

## [2.6.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.5.0...v2.6.0) (2023-06-04)


### Features

* **reports:** :sparkles: implement detailed reports ([#40](https://github.com/JamesNZL/flow-toggl-plugin/issues/40)) ([abba1eb](https://github.com/JamesNZL/flow-toggl-plugin/commit/abba1ebe72ff3f1ca254f80909687aad09b39f8f))
* **ux:** :children_crossing: improve `continue` command ([#39](https://github.com/JamesNZL/flow-toggl-plugin/issues/39)) ([1baf735](https://github.com/JamesNZL/flow-toggl-plugin/commit/1baf73598105e176433501cb1ddef8abd21069a3))
* **ux:** :children_crossing: improve autocompletion of spaces ([#44](https://github.com/JamesNZL/flow-toggl-plugin/issues/44)) ([5d80b76](https://github.com/JamesNZL/flow-toggl-plugin/commit/5d80b7687d15aaf1ab031cf2306449de31bb6e38))
* **ux:** :children_crossing: sort `continue` by latest ([#42](https://github.com/JamesNZL/flow-toggl-plugin/issues/42)) ([419f877](https://github.com/JamesNZL/flow-toggl-plugin/commit/419f8775b16a9e08490fb36962f785c441a24971))
* **ux:** :sparkles: implement deletion for all time entries ([#43](https://github.com/JamesNZL/flow-toggl-plugin/issues/43)) ([a74c5f2](https://github.com/JamesNZL/flow-toggl-plugin/commit/a74c5f264ad9a16f90557b0a0d9fb543b0373b05))
* **ux:** :sparkles: implement editing for all time entries ([#38](https://github.com/JamesNZL/flow-toggl-plugin/issues/38)) ([abf2ee3](https://github.com/JamesNZL/flow-toggl-plugin/commit/abf2ee31afa7b9b855bad6f57a47b3a4fd1ed4aa))


### Bug Fixes

* **toggl:** :goal_net: do not re-throw exceptions ([#41](https://github.com/JamesNZL/flow-toggl-plugin/issues/41)) ([e4a9863](https://github.com/JamesNZL/flow-toggl-plugin/commit/e4a9863d0a3aae89ee07621b509d8c9dcee141e6))

## [2.5.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.4.0...v2.5.0) (2023-05-27)


### Features

* **ux:** :sparkles: display time since previous stop ([#37](https://github.com/JamesNZL/flow-toggl-plugin/issues/37)) ([936390c](https://github.com/JamesNZL/flow-toggl-plugin/commit/936390c957eb975ecbefb9ac881e4d4384de29ba))


### Bug Fixes

* **reports:** :bug: proper fix for running timer in wrong daily report ([#36](https://github.com/JamesNZL/flow-toggl-plugin/issues/36)) ([08604ca](https://github.com/JamesNZL/flow-toggl-plugin/commit/08604cae31618014edcdaececf0c436f48cd85a6))

## [2.4.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.3.2...v2.4.0) (2023-05-27)


### Features

* **ux:** :children_crossing: add usage tip for entering time entry description on `start` ([#27](https://github.com/JamesNZL/flow-toggl-plugin/issues/27)) ([16a5187](https://github.com/JamesNZL/flow-toggl-plugin/commit/16a5187d3568a87ade78fb3bd8c0ec4d7cef6b29))
* **ux:** :children_crossing: warn if `tgl edit` will clear description ([#34](https://github.com/JamesNZL/flow-toggl-plugin/issues/34)) ([9e6ac38](https://github.com/JamesNZL/flow-toggl-plugin/commit/9e6ac3898960b3c1aeeb354905c32010a836c1f1))
* **ux:** :sparkles: add settings to control notifications ([#31](https://github.com/JamesNZL/flow-toggl-plugin/issues/31)) ([c1ede11](https://github.com/JamesNZL/flow-toggl-plugin/commit/c1ede118609d2d13abbe3e961e662daaa80c13af))
* **ux:** :sparkles: add settings to mute usage results ([#35](https://github.com/JamesNZL/flow-toggl-plugin/issues/35)) ([eecd08d](https://github.com/JamesNZL/flow-toggl-plugin/commit/eecd08d4d6004cc9923ea92ed75ebf2e9fd94a4e))
* **ux:** :sparkles: display elapsed time when starting at previous stop time ([#33](https://github.com/JamesNZL/flow-toggl-plugin/issues/33)) ([7f0b3d3](https://github.com/JamesNZL/flow-toggl-plugin/commit/7f0b3d331e780728acf46a606c552aae0cf63a1f))
* **ux:** :sparkles: display parsed time in `-t` result ([#32](https://github.com/JamesNZL/flow-toggl-plugin/issues/32)) ([2a50fc1](https://github.com/JamesNZL/flow-toggl-plugin/commit/2a50fc1e6ee7099f8be08c28563917d5a42e36e9))


### Bug Fixes

* **reports:** :bug: fix running timer included in wrong daily report ([#29](https://github.com/JamesNZL/flow-toggl-plugin/issues/29)) ([8942cb6](https://github.com/JamesNZL/flow-toggl-plugin/commit/8942cb6df796559ec37456a993bc38b2f3ee2bcd))

### [2.3.2](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.3.1...v2.3.2) (2023-05-21)


### Bug Fixes

* :bug: fix missing sub total time report ([#26](https://github.com/JamesNZL/flow-toggl-plugin/issues/26)) ([c12a7b6](https://github.com/JamesNZL/flow-toggl-plugin/commit/c12a7b62955c744c33ac5d03ec76c5e093d2e270))

### [2.3.1](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.3.0...v2.3.1) (2023-05-21)


### Bug Fixes

* **reports:** :bug: fix running time entry not appearing in report if spanning multiple days ([#19](https://github.com/JamesNZL/flow-toggl-plugin/issues/19)) ([20ee5a3](https://github.com/JamesNZL/flow-toggl-plugin/commit/20ee5a3435c5471c6d3327733ef2998eaefe8b01))
* **reports:** :bug: respect user's configured timezone and first day of the week ([#20](https://github.com/JamesNZL/flow-toggl-plugin/issues/20)) ([afd8b9c](https://github.com/JamesNZL/flow-toggl-plugin/commit/afd8b9c69d5309f50d197f7711e80da30d0d1a53))
* **reports:** :bug: use same single reference for `Now` ([d0b6ded](https://github.com/JamesNZL/flow-toggl-plugin/commit/d0b6ded211b291fe7e820d6e72717a119a04551f))
* **structures:** :bug: fix incorrect subgroup key ([#21](https://github.com/JamesNZL/flow-toggl-plugin/issues/21)) ([346ed9b](https://github.com/JamesNZL/flow-toggl-plugin/commit/346ed9bc1821266825a28fcf7843993aaf6b7c5b))

## [2.3.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.2.0...v2.3.0) (2023-05-20)


### Features

* **ux:** :children_crossing: add quick-launch to profile settings if bad api key ([#18](https://github.com/JamesNZL/flow-toggl-plugin/issues/18)) ([67a09a7](https://github.com/JamesNZL/flow-toggl-plugin/commit/67a09a77cb9e21065d2446be6ccc7b11303d2938))


### Bug Fixes

* **reports:** :bug: fix incorrect report window ([#17](https://github.com/JamesNZL/flow-toggl-plugin/issues/17)) ([d34733b](https://github.com/JamesNZL/flow-toggl-plugin/commit/d34733befddedd18858ce36f307db59d4892cca8))
* **ux:** :children_crossing: fix posssility of overlapping time entries when using `-t` ([#16](https://github.com/JamesNZL/flow-toggl-plugin/issues/16)) ([8a46005](https://github.com/JamesNZL/flow-toggl-plugin/commit/8a46005cda090ea964138826d753ad8fc240d15c))

## [2.2.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.1.6...v2.2.0) (2023-05-19)


### Features

* **ux:** ✨ implement reports command ([#2](https://github.com/JamesNZL/flow-toggl-plugin/issues/2)) ([#13](https://github.com/JamesNZL/flow-toggl-plugin/issues/13)) ([2203495](https://github.com/JamesNZL/flow-toggl-plugin/commit/22034956b02ed2b4d6294587b1b4b37f3dc17b0e)), closes [#11](https://github.com/JamesNZL/flow-toggl-plugin/issues/11) [#10](https://github.com/JamesNZL/flow-toggl-plugin/issues/10)


### Bug Fixes

* :bug: allow caching `null` values ([bd14f00](https://github.com/JamesNZL/flow-toggl-plugin/commit/bd14f000d0bb378ebc2a457c34bd02da2ed5f1ac))
* **reports:** :bug: do not include current timer if out of report span ([#14](https://github.com/JamesNZL/flow-toggl-plugin/issues/14)) ([4ecab3d](https://github.com/JamesNZL/flow-toggl-plugin/commit/4ecab3d2ad7167de12ac2e78cd5c74eaf0ed8226))
* **reports:** :bug: fix year span end date calculation ([ee56dbc](https://github.com/JamesNZL/flow-toggl-plugin/commit/ee56dbcc90f83863939f4c7a1b2635090d5e711b))
* **ux:** :bug: clear cache when token changes ([#15](https://github.com/JamesNZL/flow-toggl-plugin/issues/15)) ([324cbb5](https://github.com/JamesNZL/flow-toggl-plugin/commit/324cbb528ef6dd311def801d49b8ad0d1afaa94a))
* **ux:** :bug: fix `tgl edit no-project` not clearing the project ([#5](https://github.com/JamesNZL/flow-toggl-plugin/issues/5)) ([e96cac8](https://github.com/JamesNZL/flow-toggl-plugin/commit/e96cac8ce22a7ac5a83b9ef7eadd2e029b6ff7e2))
* **ux:** :bug: fix buggy token validation ([#8](https://github.com/JamesNZL/flow-toggl-plugin/issues/8)) ([3235ac9](https://github.com/JamesNZL/flow-toggl-plugin/commit/3235ac9785bfe6b476ef7ea0f06444c8a53ccf84))
* **ux:** :speech_balloon: fix incorrect time strings when 0 ([#11](https://github.com/JamesNZL/flow-toggl-plugin/issues/11)) ([45a10c3](https://github.com/JamesNZL/flow-toggl-plugin/commit/45a10c351bdd4a881647caaf9a9969a9bd2dd006))
* **ux:** :zap: fix background fetch for past time entries on `start` ([3b0b391](https://github.com/JamesNZL/flow-toggl-plugin/commit/3b0b3916288784674ceececba60a09448b47f116))

### [2.1.6](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.1.5...v2.1.6) (2023-05-16)


### Bug Fixes

* **ux:** :bug: fix `start` crash when no previous time entries ([#7](https://github.com/JamesNZL/flow-toggl-plugin/issues/7)) ([196b68e](https://github.com/JamesNZL/flow-toggl-plugin/commit/196b68ef35506c174517f36cfa04080954c1e579))

### [2.1.5](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.1.4...v2.1.5) (2023-05-16)


### Bug Fixes

* **ux:** :ambulance: do not display fractional hours ([4547aee](https://github.com/JamesNZL/flow-toggl-plugin/commit/4547aee42318d48555c7b62e999c19c65a012a2a))

### [2.1.4](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.1.3...v2.1.4) (2023-05-16)


### Bug Fixes

* **ux:** :bug: fix incorrect time strings when longer than 1 day ([#6](https://github.com/JamesNZL/flow-toggl-plugin/issues/6)) ([58ed0e3](https://github.com/JamesNZL/flow-toggl-plugin/commit/58ed0e351ed43c8be6d5278eecf9332d930b2b36))

### [2.1.3](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.1.2...v2.1.3) (2023-05-15)


### Bug Fixes

* **ux:** :speech_balloon: fix casing of `"No Project"` to match toggl ([65ebf8d](https://github.com/JamesNZL/flow-toggl-plugin/commit/65ebf8de721730959501ede175f2a1c515ac96a8))

### [2.1.2](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.1.1...v2.1.2) (2023-05-14)


### Bug Fixes

* **ux:** :bug: ensure stop time is not before start ([b969773](https://github.com/JamesNZL/flow-toggl-plugin/commit/b9697733409259b0de17a7ef241ee7f01a26fc08))
* **ux:** :bug: fix wrong elapsed time calculation when using `-t` ([#3](https://github.com/JamesNZL/flow-toggl-plugin/issues/3)) ([7fcfa85](https://github.com/JamesNZL/flow-toggl-plugin/commit/7fcfa85e3c0316e97c9fd37c3a1b09b53202352d))

### [2.1.1](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.1.0...v2.1.1) (2023-05-12)


### Bug Fixes

* :zap: do not verify api token on `tgl refresh` command ([2489a7b](https://github.com/JamesNZL/flow-toggl-plugin/commit/2489a7b856be9df321e2048496fe83cd6a366574))

## [2.1.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.0.1...v2.1.0) (2023-05-12)


### Features

* **ux:** :children_crossing: refresh cache on plugin initialisation ([b6b38a2](https://github.com/JamesNZL/flow-toggl-plugin/commit/b6b38a2cb072353b59b639039629424a56b1c92d))

### [2.0.1](https://github.com/JamesNZL/flow-toggl-plugin/compare/v2.0.0...v2.0.1) (2023-05-12)


### Bug Fixes

* **ux:** :bug: fully fix ignored sanitisation of `-t` from other results ([4789d71](https://github.com/JamesNZL/flow-toggl-plugin/commit/4789d714214e30fb4c849792b7a638b9c7d88fc6))

## [2.0.0](https://github.com/JamesNZL/flow-toggl-plugin/compare/v1.8.0...v2.0.0) (2023-05-12)


### ⚠ BREAKING CHANGES

* **ux:** this changes behaviour from same user input

use `-t -5` to move the start/stop time backwards in time

use `-t 5` to move the start/stop time forwards in time

previously, `tgl start` and `tgl stop` would treat `-t 5` as moving backwards, and vice-versa

### Features

* **ux:** :children_crossing: use consistent positive/negative time span parsing ([1d489c4](https://github.com/JamesNZL/flow-toggl-plugin/commit/1d489c448464c27aaa5b01c29ce0cb2a22c8ac00))
* **ux:** :sparkles: allow editing start time in `tgl edit` ([e4dc303](https://github.com/JamesNZL/flow-toggl-plugin/commit/e4dc3039c4a6472c0549127b316fd27aa9140577))


### Bug Fixes

* **ux:** :bug: do not sanitise `-t` flag from description of standard `tgl start` ([1d6720a](https://github.com/JamesNZL/flow-toggl-plugin/commit/1d6720a4b7abe13851af74703f92b91a915372fc))
* **ux:** :bug: fix broken autocomplete of time span flag usage example ([a8e3c7d](https://github.com/JamesNZL/flow-toggl-plugin/commit/a8e3c7d04b78d9cdee76b3296dd59cc0bd7edbad))

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
