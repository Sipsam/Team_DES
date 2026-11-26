# Twin Edge V3 development roadmap

Use this page for ordering and dependencies. [CURRENT_TASK](CURRENT_TASK.md) holds the active checkpoint and resume action. The [approved design](../GameDesign/TwinEdge_Latest_GamePlanning_IntegratedSummary.pdf) defines gameplay intent. Its historical milestones and open design questions do not authorize the next implementation. The [validation summary](../Playtest/ValidationSummary.md) records actual run coverage.

| ID | Task and priority | State | Dependencies and completion | Commit and deferred work |
| --- | --- | --- | --- | --- |
| V3-LEVEL-MOUNTAIN | Finish approved DeepGullies terrain and FinalPolish materials · first | `COMPLETED` | Scoped evidence reviewed, pushed to `main`, and all 14 official D3D11 views republished after push. | `6498cd4`. A Windows Player packing both approved assets and the new full integration run belong to V3-INTEGRATION. |
| V3-WORKFLOW | Establish the repository-native workflow | `COMPLETED` | After V3-LEVEL-MOUNTAIN. Static workflow and recovery checks passed and remote main was verified. | `ea911d5`. Nonvisual. |
| V3-WORKFLOW-V2 | Add lightweight task-centric orchestration | `COMPLETED` | Scoped L0 and ten document dry runs passed. | `97611f3`. Nonvisual. |
| V3-FEEDBACK-01 | Checkpoint and Recovery Improvements · Feedback 1 | `COMPLETED` | Validated the three recovery locations and direct peer regressions. | `7f16d56`. Full E2E Relay deferred. |
| V3-FEEDBACK-02 | Camera-relative Controls and Movement · Feedback 4, 5 | `COMPLETED` | Scoped D3D11 Relay controls and recovery passed. Official map and runtime views captured. | `c512ce7`. Full E2E Relay deferred. |
| V3-FEEDBACK-03 | Cooperative Pole Traversal · Feedback 3, 6 | `COMPLETED` | Isolated D3D11 Relay, saved Collider routes and official map and runtime views passed. | `c2b464b`. Full integration deferred. |
| V3-FEEDBACK-04 | Mountain Geometry and Launch Prediction · Feedback 7, 26 | `COMPLETED` | Saved summit supports, isolated Relay pole and Return routes and owner launch prediction passed. Official map and owner views captured. | `33b7fcd`. Full integration deferred. |
| V3-FEEDBACK-05 | Player Appearance and PvP · Feedback 8, 9, 10, 11, 27 | See [CURRENT_TASK](CURRENT_TASK.md) | Verify cape identity, contest poses, grab movement and camera, hit range and gem behavior in isolated peers. | See active checkpoint. |
| V3-FEEDBACK-06 | Collapsing Bridge and Stairway · Feedback 2, 25 | `PLANNED` | After V3-FEEDBACK-05, when authorized. | Pending. |
| V3-FEEDBACK-07 | River and Raft Gameplay · Feedback 12, 13, 24 | `PLANNED` | After V3-FEEDBACK-06, when authorized. | Pending. |
| V3-FEEDBACK-08 | Castle Environment and Puzzle UX · Feedback 14–19 | `PLANNED` | After V3-FEEDBACK-07, when authorized. | Pending. |
| V3-FEEDBACK-09 | Dungeon and Sewer Puzzle · Feedback 20–23 | `PLANNED` | After V3-FEEDBACK-08, when authorized. | Pending. |
| V3-INTEGRATION | Final integration and release build · last | `DEFERRED` | All approved major development complete and user explicitly starts integration. Run whole-world and cross-system regression, full E2E Relay/full match, the latest combined DeepGullies/FinalPolish Windows Player check, and validate the final distribution build. Use the outstanding human playtest and platform checks in Validation Summary to plan coverage. | Pending. |

Start the next feedback task only on user request.
