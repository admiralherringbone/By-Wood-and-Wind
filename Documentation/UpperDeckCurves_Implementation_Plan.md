# Implementation Plan: Upper Decks Rework

## 1. Overview & Architectural Principles

This implementation plan details the sequential tasks required to bring the **`UpperDeckCurves`** subsystem in **`By Wood and Wind`** to a verified, production-ready state.

### Supreme Architectural Rule: TopLine as the Cap Rail
- **`TopLineCurves` IS the Cap Rail / Gunwale**: `TopLineCurves` represents the physical upper ceiling of the hull and the top sheer rail of the vessel running continuously from stem to stern.
- **Upper Decks are Enclosed Corridors**: Tier 1 upper decks (Forecastle, Quarterdeck, Gangways) live strictly **underneath / inside** the vertical envelope bounded between `MainDeckCurves` (floor datum) and `TopLineCurves` (ceiling datum / cap rail).
- **Natural Bulwarks**: The bulwark height at any station $z$ is the natural vertical distance up to `TopLineCurves`:
  $$\text{Bulwark Height}(z) = Y_{\text{TopLine}}(z) - Y_{\text{UpperDeck}}(z)$$
- **No Upward Protrusions**: Because `TopLineCurves` forms the cap rail, **no hull components extend above TopLine**. Any historical upward extensions added to the Transom Rake, Stem Post, Fashion Timbers, or Body Curves to catch hypothetical higher bulwarks are obsolete and must be cleanly pruned.

---

## 2. Phase Flowchart

```text
┌────────────────────────────────────────────────────────┐
│ Phase 1: Pruning Obsolete Upward Extensions            │
│ - Remove FashionCurves P5/P6 bulwark corner extensions │
│ - Remove SheerCurves transom rake & stem extensions    │
│ - Remove BodyCurves P5/P6 bulwark extension nodes      │
│ - Clean UpperDeckCurves above-TopLine extrapolation    │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Phase 2: Scene & Datum Baseline Verification           │
│ - Validate GameObject setup & serialized references    │
│ - Verify default Tier 1 instantiation & zero-GC buffers│
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Phase 3: Boundary Conformance & Safeguard Validation   │
│ - Test stem/transom flush magnetic snapping            │
│ - Test "No Floating Decks" Tier 2 footprint clamping   │
│ - Test vertical clearance safeguard thresholds         │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Phase 4: Geometry & Visual Fidelity Tuning             │
│ - Proportional camber drop verification                │
│ - Solid dual-surface & edge cap alignment              │
│ - Waist breastwork bulkhead & stanchion grounding      │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Phase 5: Downstream Lofting & Cross-Script Integration │
│ - SectionModelCurves framing integration               │
│ - Transverse frame upper deck clamp notches            │
│ - Strakes/ribband clearance alignment                  │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Phase 6: Presets, QoL & Git Deployment                │
│ - Vessel preset configurations (Frigate, Sloop, SOL)   │
│ - Interactive handle sensitivity & UX polish           │
│ - Commit & sync to GitHub origin/main                  │
└────────────────────────────────────────────────────────┘
```

---

## 3. Detailed Phase Breakdown

### Phase 1: Pruning Obsolete Upward Extensions & Restoring Pure TopLine Boundaries

Before tuning upper deck geometry, we must strip away the redundant upward extension code that was added under the outdated assumption that bulwarks would protrude above TopLine.

| Task ID | Component | Cleanup Scope | Success Criteria |
| :--- | :--- | :--- | :--- |
| **1.1** | **`FashionCurves.cs`** | • Remove `stbdBulwarkP5`, `stbdBulwarkP6`, `innerStbdBulwarkP5`, `innerStbdBulwarkP6`.<br>• Remove `hasTier1BulwarkCorner` and `hasTier2BulwarkCorner`.<br>• Remove extension lines, transverse bulwark crossbars, corner spheres, and Scene GUI labels that draw upward past $P_4$.<br>• Remove fallback bulwark corner interpolation in `GetPointAtZ` and `GetInnerFashionPointAtZ`. | Fashion Timbers terminate cleanly and naturally at TopLine ($P_4$ / `stbdP4` and `innerStbdP4`). Zero extension lines drawn above $P_4$. |
| **1.2** | **`SheerCurves.cs`** | • Remove `transomBulwarkP1`, `transomBulwarkP2`, `stemBulwarkP1`, `stemBulwarkP2`.<br>• Remove `hasStemBulwark1/2` and `hasTransomBulwark1/2`.<br>• Remove upward linear rake continuation past `transomP1` in `GetSternProfileZAtY()`.<br>• Remove bulwark extension Gizmo lines, spheres, and Handles labels at the bow stem and stern transom. | Transom Rake terminates cleanly at `transomP1` (TopLine). Stem profile terminates cleanly at `stemP3` / `upperStemTop`. |
| **1.3** | **`BodyCurves.cs`** | • Remove $P_5$ and $P_6$ bulwark top extension nodes (`cachedP5`, `cachedP6`, `cachedInnerP5`, `cachedInnerP6`).<br>• Remove `hasUpperBulwark`, `hasUpperBulwarkAtMidships`, and `hasTier2Bulwark`.<br>• Remove topsides tumblehome slope extrapolation logic past $P_4$. | The master hull section curve terminates cleanly at $P_4$ (TopLine). TopLine ($P_4$) is restored as the absolute highest point of `BodyCurves`. |
| **1.4** | **`UpperDeckCurves.cs`** | • In `GetInnerSternZAtElevation()`, remove the slope extrapolation block that projects above `fashionCurves.innerStbdP4.y`.<br>• Align bulwark cap rail queries directly with `TopLineCurves`.<br>• Ensure `UpperDeckCurves` treats `TopLineCurves` as the immutable upper ceiling. | All deck boundary queries strictly conform to TopLine and fashion timber inner lines with zero extrapolation above $P_4$. |
| **1.5** | **Compiler & Scene Audit** | • Verify Unity project compiles with zero errors.<br>• Confirm `Matched curves.unity` Scene view displays a clean hull profile without stray extension lines above TopLine. | Clean compile, no null reference warnings, clean Scene gizmos. |

---

### Phase 2: Scene & Datum Baseline Verification

| Task ID | Item | Description | Success Criteria |
| :--- | :--- | :--- | :--- |
| **2.1** | **Scene Hierarchy Check** | Verify GameObject `UpperDeckCurves` in `Matched curves.unity`. | Component exists, is active, and is properly referenced by downstream scripts. |
| **2.2** | **Datum References** | Verify linkages to `MainDeckCurves`, `TopLineCurves`, `SheerCurves`, `BreadthCurves`, `FashionCurves`, `BodyCurves`, `FloorCurves`, `BilgeCurves`, `BackboneCurves`. | All references populated; no `null` warnings in console during `EnsureInitialized()`. |
| **2.3** | **Default Tier Instantiation** | Trigger `Reset to Default Upper Decks` via Inspector context menu. | Generates **Tier 1 (Upper Decks)** with two enabled segments: **Forecastle** and **Quarterdeck**, with an open waist in between. |
| **2.4** | **Buffer Allocation** | Inspect runtime buffers (`cachedCenterlinePts`, `cachedDeckBeams`, etc.). | Arrays allocate once at `RESOLUTION = 48`; zero garbage collection allocations during continuous curve updates. |

---

### Phase 3: Boundary Conformance & Safeguard Validation

| Task ID | Item | Description | Success Criteria |
| :--- | :--- | :--- | :--- |
| **3.1** | **Stem Flush Snapping** | Drag Forecastle forward cone handle toward the bow. | Within $0.05\text{m}$ of the inner stem post, handle magnetically snaps flush to stem with zero gap; `flushToStem` toggles to `true`. |
| **3.2** | **Transom Flush Snapping** | Drag Quarterdeck aft cone handle toward the stern. | Within $0.05\text{m}$ of the inner fashion timbers, handle snaps flush to transom rake; `flushToTransom` toggles to `true`. |
| **3.3** | **One-Click Snap Buttons** | Click `⚡ Snap Forecastle Flush to Stem` and `⚡ Snap Quarterdeck Flush to Transom`. | Instantly locks extremities flush to inner hull boundaries without manual handle dragging. |
| **3.4** | **"No Floating Decks" (Tier 2)** | Click `+ Add Poop Deck (Tier 2)`. Drag the supporting Quarterdeck forward break aft. | Poop Deck forward boundary automatically shrinks/clamps backward so it never hovers over the open waist ($Z_{\text{fwd}}^{\text{Poop}} \le Z_{\text{fwd}}^{\text{QD}}$). |
| **3.5** | **Vertical Space Safeguard** | Lower `TopLineCurves` or raise `MainDeckCurves` until remaining clearance $< 0.68\text{m}$. | Inspector displays warning HelpBox; deck creation is blocked until clearance is restored. |

---

### Phase 4: Geometry & Visual Fidelity Tuning

| Task ID | Item | Description | Success Criteria |
| :--- | :--- | :--- | :--- |
| **4.1** | **Proportional Camber** | Inspect deck beam curvature at midships vs. narrow bow/stern stations. | Parabolic camber crown smoothly tapers in proportion to local hull beam; no excessive lateral dip at narrow extremities. |
| **4.2** | **Dual Solid Planking** | Toggle `showBottomDeck` and adjust `deckThickness` ($0.10\text{m}\text{--}0.30\text{m}$). | Top walking deck and lower ceiling surface render cleanly separated by specified thickness; edge cap lines join both surfaces along the hull. |
| **4.3** | **Waist Breastwork Bulkheads** | Inspect transverse bulkheads at Forecastle aft break and Quarterdeck forward break. | Transverse arcs match upper deck camber and ground down directly into the `MainDeckCurves` walking surface. |
| **4.4** | **Breastwork Stanchions** | Adjust `breastworkStanchionCount` ($2\text{--}16$). | Vertical framing stanchions distribute evenly across the bulkhead opening connecting top and bottom arcs. |
| **4.5** | **Bulwark Rail Conformance** | Verify bulwarks on Tier 1 upper decks. | Bulwarks smoothly conform to `TopLineCurves` as their true cap rail. |

---

### Phase 5: Downstream Lofting & Cross-Script Integration

| Task ID | Item | Description | Success Criteria |
| :--- | :--- | :--- | :--- |
| **5.1** | **`SectionModelCurves` Integration** | Verify calls to `upperDeckCurves.HasBulwarkAtZ(z, ...)` in `SectionModelCurves.cs`. | Transverse hull sections extend upward to TopLine across the entire ship, with upper deck clamp cutouts cleanly positioned. |
| **5.2** | **Deck Clamp / Shelf Alignment** | Compare upper deck edge points with transverse station frame cuts. | Waterway edges sit on the inner frame face without puncturing outer hull planking. |
| **5.3** | **Waist Gangway Integration** | Click `+ Add Midship Gangway` on Tier 1. | Creates narrow catwalks over the waist connecting Forecastle and Quarterdeck without obstructing centerline waist openings. |
| **5.4** | **Framing / Strakes Clearance** | Verify clearance between upper deck levels and gunport strake bands in `StrakesModel.cs`. | Upper deck levels align with intended gunport sills (e.g., quarterdeck carronades / forecastle chasers). |

---

### Phase 6: Presets, QoL & Git Deployment

| Task ID | Item | Description | Success Criteria |
| :--- | :--- | :--- | :--- |
| **6.1** | **Frigate Layout Preset** | Standard 2-deck layout: Forecastle ($Z = 14\text{m} \to 8\text{m}$), Open Waist ($Z = 8\text{m} \to -2\text{m}$), Quarterdeck ($Z = -2\text{m} \to -12.5\text{m}$). | Verified visually in Scene view. |
| **6.2** | **Ship-of-the-Line Preset** | Multi-tier layout: Forecastle + Quarterdeck on Tier 1, Poop Deck on Tier 2. | Verified visually in Scene view. |
| **6.3** | **Scene View Interaction Polish** | Test dragging handles in 3D perspective and orthographic views. | Handles move smoothly with full Undo/Redo support (`Cmd+Z`). |
| **6.4** | **Git Version Control** | Stage, commit, and push all verified code, scene updates, and documentation. | Working tree clean, synced to `origin/main` on GitHub. |

---

## 4. Immediate Execution Checklist (Phase 1)

- [x] **Step 1.1**: Clean up `FashionCurves.cs` (remove $P_5/P_6$ bulwark corners, extension lines, transverse crossbars, and above-$P_4$ interpolation).
- [x] **Step 1.2**: Clean up `SheerCurves.cs` (remove transom rake extensions `transomBulwarkP1/P2` and stem extensions `stemBulwarkP1/P2`).
- [x] **Step 1.3**: Clean up `BodyCurves.cs` (remove $P_5/P_6$ bulwark nodes, restore $P_4$ as supreme top node).
- [x] **Step 1.4**: Clean up `UpperDeckCurves.cs` (remove above-TopLine transom extrapolation, align cap rail directly with `TopLineCurves`).
- [x] **Step 1.5**: Verify compilation in Unity and check Scene view.
- [x] **Step 1.6**: Commit Phase 1 cleanup to Git.

---

## 5. Execution Checklist (Phase 2: Scene & Datum Baseline Verification)

- [x] **Step 2.1**: **Scene Hierarchy Check** — Verified GameObject `UpperDeckCurves` (`fileID: 2091202171`) in `Matched curves.unity` is active (`m_IsActive: 1`) and downstream components (`SectionModelCurves`) are linked to it.
- [x] **Step 2.2**: **Datum References** — Verified all 9 datum references (`mainDeckCurves`, `topLineCurves`, `sheerCurves`, `breadthCurves`, `bilgeCurves`, `floorCurves`, `fashionCurves`, `bodyCurves`, `backboneCurves`) are populated in `Matched curves.unity` and `FindReferences()` has fallback resolution.
- [x] **Step 2.3**: **Default Tier Instantiation** — Verified `InitializeDefaultTiers()` cleanly configures Tier 1 (Forecastle flush to inner stem, Quarterdeck flush to inner transom, open waist in between).
- [x] **Step 2.4**: **Buffer Allocation & Zero GC** — Profiled and optimized `BuildSegmentDeckBeams`, `BuildSegmentBreastworks`, and `BuildTierBulwarks` to pool and reuse `Vector3[]` buffers (`cachedDeckBeams`, `cachedBottomDeckBeams`, `forwardBreastworkStanchions`, `aftBreastworkStanchions`, `cachedBulwarkStanchions`), eliminating frame-by-frame heap allocations during curve updates.

---

## 6. Execution Checklist (Phase 3: Boundary Conformance & Safeguards)

- [x] **Step 3.1**: **Stem Flush Snapping** — Moving Forecastle forward handle within $0.05\text{m}$ of inner stem magnetically snaps flush; toggles `flushToStem = true`, zeroes `stemClearance`.
- [x] **Step 3.2**: **Transom Flush Snapping** — Moving Quarterdeck aft handle within $0.05\text{m}$ of inner fashion timber / transom rake magnetically snaps flush; toggles `flushToTransom = true`, zeroes `sternClearance`.
- [x] **Step 3.3**: **One-Click Snap Buttons** — Integrated `⚡ Snap Forecastle Flush to Stem` and `⚡ Snap Quarterdeck Flush to Transom` in Custom Inspector and Context Menu for instant boundary alignment.
- [x] **Step 3.4**: **"No Floating Decks" (Tier 2 Safeguard)** — Enforced strict geometric clamp where Poop Deck forward boundary never exceeds supporting Quarterdeck break ($Z_{\text{fwd}}^{\text{Poop}} \le Z_{\text{fwd}}^{\text{QD}}$); moving Quarterdeck forward break aft automatically retracts Poop Deck footprint.
- [x] **Step 3.5**: **Vertical Space Safeguard** — Monitored vertical clearance between `MainDeckCurves` and `TopLineCurves`; when clearance $< 0.68\text{m}$, Inspector displays warning HelpBox and disables deck preset creation buttons.


