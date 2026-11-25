# Twin Edge V3 development roadmap

Use this page for ordering and dependencies. [CURRENT_TASK](CURRENT_TASK.md) holds the active checkpoint and resume action. The [approved design](../GameDesign/TwinEdge_Latest_GamePlanning_IntegratedSummary.pdf) defines gameplay intent. Its historical milestones and open design questions do not authorize the next implementation. The [validation summary](../Playtest/ValidationSummary.md) records actual run coverage.

| ID | Task and priority | State | Dependencies and completion | Commit and deferred work |
| --- | --- | --- | --- | --- |
| V3-LEVEL-MOUNTAIN | Finish approved DeepGullies terrain and FinalPolish materials · first | `COMPLETED` | Scoped evidence reviewed, pushed to `main`, and all 14 official D3D11 views republished after push. | `6498cd4`. A Windows Player packing both approved assets and the new full integration run belong to V3-INTEGRATION. |
| V3-WORKFLOW | Establish the repository-native workflow | `COMPLETED` | After V3-LEVEL-MOUNTAIN. Static workflow and recovery checks passed and remote main was verified. | `ea911d5`. Nonvisual. |
| V3-WORKFLOW-V2 | Add lightweight task-centric orchestration · current | See [CURRENT_TASK](CURRENT_TASK.md) | After V3-WORKFLOW. Verify environment capabilities, routing, recovery and publication through scoped L0 and ten document dry runs. | Resolve the commit through Git and the optional local record. Nonvisual. |
| V3-INTEGRATION | Final integration and release build · last | `DEFERRED` | All approved major development complete and user explicitly starts integration. Run whole-world and cross-system regression, full E2E Relay/full match, the latest combined DeepGullies/FinalPolish Windows Player check, and validate the final distribution build. Use the outstanding human playtest and platform checks in Validation Summary to plan coverage. | Pending. |

**Next game development task** is undecided. The validation summary lists outstanding playtest questions and platform checks. Wait for user direction before starting another gameplay, level or visual change.
