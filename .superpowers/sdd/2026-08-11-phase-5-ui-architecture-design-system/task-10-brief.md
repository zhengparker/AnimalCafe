### Task 10: Migrate MainCafe and Preserve Phase 0 Contracts

**Files:** `Assets/Editor/Phase0SceneSetup.cs`, `Assets/Scripts/UI/TimeControlPanel.cs`, generated `Assets/Scenes/MainCafe.unity`, and focused Phase 0/Phase 5 tests.

**Cases:** IT-028, IT-029, RT-004 through RT-007, RT-013; prepare evidence hooks for MT-030, MT-031, and MT-034.

- [ ] Write failing hierarchy and behavior tests for one Phase 5 `UI Root`, TMP/theme time controls, one EventSystem, and one of each existing Phase 0 service.
- [ ] Migrate the legacy `Phase0_TimeControls` presentation under the canonical Phase 5 root while preserving Pause/Normal/Fast listeners and world selection behavior.
- [ ] Run deterministic setup twice and prove it does not duplicate UI, EventSystem, camera input, scene interaction, or GameTime service.
- [ ] Keep MainCafe as the only production Build Settings scene; do not change validation-scene scope.
- [ ] Record automated evidence and manual-review preparation only; do not claim MT-030, MT-031, or MT-034 as performed.
