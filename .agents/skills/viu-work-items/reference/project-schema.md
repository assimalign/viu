# Viu GitHub Project — schema & manual recipes

Reference for the `viu-work-items` skill. Captured from the live project on 2026-07-16.
The helper script resolves IDs **dynamically**, so these are for the manual path and for
understanding the model. If an ID stops working, re-run the discovery commands at the bottom.

## Coordinates

| Thing | Value |
| --- | --- |
| Repo | `assimalign/viu` |
| Org / owner | `assimalign` |
| Project | **#15 "Viu"** |
| Project node id | `PVT_kwDOA9eCcc4BdkOB` |

> **History:** on 2026-07-16 this board held ~406 stray `assimalign/cohesion` items (`[Lxx.yy...]`
> codes); they were removed the same day (project items only — the issues remain in the cohesion
> repo and on its board #13). If cohesion items reappear, check the project's auto-add workflow
> (⋯ menu → Workflows, UI-only) and never modify them from this repo.

## WBS taxonomy

Work items carry their position in the title as `[<code>] <description>`. The hierarchy is held by
**native GitHub parent/sub-issue links**, and every item is added to Project #15.

| Code shape | Segments | Level | Example | Parent |
| --- | --- | --- | --- | --- |
| `V01.01.00` | 3 (`.00`) | Program root | `[V01.01.00] Viu - Framework Libraries` | — |
| `V01.01.NN` | 3 | **Area epic** | `[V01.01.02] Framework - Reactivity` | program root |
| `V01.01.NN.MM` | 4 | **Feature** | `[V01.01.02.04] Implement Computed<T>…` | area epic |
| `V01.01.NN.MM.PP` | 5 | **Task** | `[V01.01.02.04.01] …` | feature |

`V` = Viu program; `V01.01` = Framework Libraries. Area epics are titled `Framework - <Area>`.

Branch convention: `feature/<wbs>-<slug>` (e.g. `feature/V01.01.02.01-dependency-engine`). The WBS
in the branch names the **feature** currently in flight.

### Current area epics (parents for new sibling features)

| Issue | Code | Area |
| --- | --- | --- |
| #2  | V01.01.01 | Shared |
| #6  | V01.01.02 | Reactivity |
| #16 | V01.01.03 | RuntimeCore |
| #38 | V01.01.04 | RuntimeDom |
| #47 | V01.01.05 | Compiler |
| #56 | V01.01.06 | Sfc |
| #63 | V01.01.07 | ServerRenderer |
| #69 | V01.01.08 | Router |
| #75 | V01.01.09 | Store |
| #80 | V01.01.10 | DevTools |
| #84 | V01.01.11 | Testing |
| #89 | V01.01.12 | Tooling |
| #97 | V01.01.13 | Documentation |

(Program root: `#1 [V01.01.00] Viu - Framework Libraries`.)

(Re-list with: `gh issue list --repo assimalign/viu --state open --search '"Framework -" in:title' --json number,title`)

## Custom fields (single-select)

The script resolves these **by name** at runtime, so the ids below are only for the manual path.

| Field | Field id | Options (name = optionId) |
| --- | --- | --- |
| **Status** | `PVTSSF_lADOA9eCcc4BdkOBzhYE3bA` | Backlog=`f75ad846`, Ready=`e18bf179`, In progress=`47fc9ee4`, In review=`aba860b9`, Done=`98236657` |
| **Priority** | `PVTSSF_lADOA9eCcc4BdkOBzhYIM7A` | P001=`b5956103`, P002=`dfdbcf69`, P003=`95477bb9`, P004=`817a97af`, P005=`cda6227b`, P006=`505d31cd`, P007=`304897db` |
| **Wave** | `PVTSSF_lADOA9eCcc4BdkOBzhYIM8s` | W01=`0bfcc55b`, W02=`62249a43`, W03=`185ba181`, W04=`970e7757`, W05=`f644717c`, W06=`82a64ada` |
| **Kind** | `PVTSSF_lADOA9eCcc4BdkOBzhYINAA` | Program=`d433146c`, Area Epic=`ac4733aa`, Feature=`21f1a1bb`, Task=`b2e20b3b` |
| **Area** | `PVTSSF_lADOA9eCcc4BdkOBzhYINCE` | Shared=`2ade358b`, Reactivity=`84656c3d`, RuntimeCore=`6913b381`, RuntimeDom=`844d5ae7`, Compiler=`4bff0e3b`, Sfc=`87ae4806`, ServerRenderer=`654171ca`, Router=`3e01c63e`, Store=`f18fc7f6`, DevTools=`9f708a9b`, Testing=`9ccd7976`, Tooling=`f1e7403a`, Documentation=`b314fb0d` |
| **Origin** | `PVTSSF_lADOA9eCcc4BdkOBzhYINA4` | Planned=`da350ff4`, DiscoveredTask=`7257dbe0`, DiscoveredFeature=`91f5bfbe` |

The script sets **Status, Kind, Area, Origin** on every item it creates (plus Priority/Wave when
passed). `Kind` comes from the WBS depth (Feature/Task), `Area` from the area-epic ancestor's
`Framework - X` title, `Origin` from the scope-creep classification. Discovered items also get the
**`scope-creep`** repo label (color `FBCA04`).

> Also present but not managed by the skill: **Size** (XS–XL, a project-template leftover — prefer
> the numeric **Estimate** field if sizing is ever wanted), **Estimate** (number), **Iteration**
> (14-day sprints starting 2026-07-16). A GitHub-managed org-level `Priority` field with zero
> options existed on the project and was deleted on 2026-07-16; the Priority above is a normal
> project custom field like Cohesion's.

Wave semantics: W01 rendering foundation → W02 component model → W03 compiler and SFC →
W04 ecosystem (router/store/built-ins) → W05 server rendering + developer experience →
W06 enterprise polish.

### Counting scope creep

```bash
# By label (issues CLI):
gh issue list --repo assimalign/viu --label scope-creep --state all --json number,title,state
# By field on the board: filter or group Project #15 by Origin (DiscoveredTask / DiscoveredFeature).
```

## Body template (repo standard)

```markdown
## Summary
- <one or two sentences: what and why; name the Vue 3 counterpart. For scope-creep items, note it was discovered out of scope.>

## Acceptance Criteria
- <observable, testable outcome>
- Tests cover the new behavior.
- The implementation remains trimming-safe and WASM/NativeAOT-compatible.

### Standards and Compliance
- <Vue 3 API parity reference (vuejs.org / vuejs/core), WHATWG/W3C spec for DOM behavior, or a note that it is a Viu runtime-contract concern>
```

## Manual recipe (when not using the helper script)

```bash
REPO=assimalign/viu ; OWNER=assimalign ; PROJ=15
PROJECT_ID=PVT_kwDOA9eCcc4BdkOB

# 1. Find the parent + its node id (feature for a task, area epic for a sibling feature)
gh issue view 14 --repo $REPO --json number,title,id

# 2. Find the next free child number. Do NOT use --search for the dotted code (it silently drops
#    siblings). Fetch all issues and filter on the title with gh's built-in jq (-q):
gh issue list --repo $REPO --state all --limit 5000 --json number,title \
  -q '.[] | select(.title | test("^\\[V01\\.01\\.02\\.01\\.[0-9]{2}\\]")) | .title'
#    Then take the max trailing NN across OPEN and CLOSED, add 1, zero-pad to two digits.

# 3. Create the issue
URL=$(gh issue create --repo $REPO \
  --title '[V01.01.02.01.01] <short imperative description>' \
  --body-file body.md)
NUM=${URL##*/}

# 4. Add to project, capture the project item id
ITEM=$(gh project item-add $PROJ --owner $OWNER --url "$URL" --format json --jq .id)

# 5. Set fields (repeat with the ids from the table above): Status, Kind, Area, Origin, [Priority, Wave]
gh project item-edit --id "$ITEM" --project-id $PROJECT_ID \
  --field-id PVTSSF_lADOA9eCcc4BdkOBzhYE3bA --single-select-option-id 47fc9ee4   # Status = In progress

# 6. Link as a native sub-issue of the parent
PARENT_ID=$(gh issue view 14 --repo $REPO --json id --jq .id)
CHILD_ID=$(gh issue view "$NUM" --repo $REPO --json id --jq .id)
gh api graphql -f query='mutation($p:ID!,$c:ID!){ addSubIssue(input:{issueId:$p, subIssueId:$c}){ subIssue { number } } }' \
  -F p="$PARENT_ID" -F c="$CHILD_ID"
```

## Closing multiple work items from one PR

GitHub only auto-closes an issue when the PR body contains a **closing keyword + that issue's number**.
A single `Closes #1, #2` links only the first. Use **one keyword per issue, one per line**:

```
Closes #14
Closes #15
Closes #16
```

Closing a parent feature does **not** close its sub-issues, and closing every sub-issue does **not**
close the parent. List each work item the PR actually resolves.

The repo has a **PR template** (`.github/pull_request_template.md`) with a "Work items resolved" block,
and **issue forms** (`.github/ISSUE_TEMPLATE/feature.yml`, `task.yml`, `scope-creep.yml`) as a
no-PowerShell fallback. The forms can't compute the next WBS code — prefer the script when exact
placement matters.

## Built-in Project workflows — enable in the UI (one-time, not API-automatable)

GitHub's built-in Project automations are **not** exposed by the REST/GraphQL API or `gh` (only
`deleteProjectV2Workflow` exists), so this is the one step that must be done by hand. At
`https://github.com/orgs/assimalign/projects/15` → **⋯ menu → Workflows**, enable:

| Workflow | Set |
| --- | --- |
| Item added to project | Status → **Backlog** |
| Item closed | Status → **Done** |
| Pull request merged | Status → **Done** |
| Item reopened | Status → **In progress** |
| Auto-add sub-issues to project | on |

With these on, closing/merging a PR moves every work item it `Closes` to Done automatically, so the
script's `-Status` only needs to seed the initial state.

## Re-discovering IDs if the schema changes

```bash
# Project node id
gh project view 15 --owner assimalign --format json --jq .id
# All single-select fields with their option ids
gh project field-list 15 --owner assimalign --format json \
  --jq '.fields[] | select(.type=="ProjectV2SingleSelectField") | {name, id, options:[.options[]|{name,id}]}'
```
