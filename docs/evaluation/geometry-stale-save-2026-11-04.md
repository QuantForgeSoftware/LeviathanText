# Guarded line geometry saves

The existing line geometry PUT accepts an optional `expectedGeometry` containing the previously loaded polygon and baseline arrays. The server compares both ordered point sequences against the current line before writing. A mismatch returns HTTP 409 with `revisionConflict` and leaves geometry history intact. Requests that omit the field keep the previous behavior. The project version check still protects changes that race between the API read and store write.

The Angular editor sends the loaded geometry and a separate submitted draft. A successful reply advances the saved snapshot for that line to the submitted values. Drafts on other lines and newer edits to the same line remain dirty. A conflict keeps the draft and displays a status message, including when a newly drawn box places an existing unplaced line. Load generations prevent a delayed reply from affecting the editor after the user leaves and returns to a surface. Geometry ordering and structural edits are separate collaboration operations.

The independent resize correction keeps the opposite bounding-box corner fixed for all four handles. Each moving axis clamps to the correct side of the fixed corner, within the frame and with four pixels of separation where space permits.

| Verification | Evidence |
| --- | --- |
| API baseline | Both stale requests returned HTTP 200 instead of HTTP 409, one with changed polygon point order and one with changed baseline point order. |
| API candidate | 91 tests passed in the non-fitting suite, including legacy and guarded geometry plus unchanged history on conflict. Isolated Release build succeeded with zero warnings and errors. |
| Browser baseline | Four guarded-save tests reproduced the stale payload, draft tracking and delayed reply failures. Both independent corner tests failed before the resize correction. |
| Browser candidate | All 84 Angular tests passed. The production build succeeded in an isolated output directory. |

Local logs and build receipts are in `.artifacts/experiments/geometry-conflict-v1.al2vNF/`. The initial isolated API baseline invocation exposed a missing test-fixture path. The repeated baseline used the original repository fixture at the expected relative path. The first Angular production build exited during sandboxed worker startup. The isolated rerun completed with local build workers enabled.
