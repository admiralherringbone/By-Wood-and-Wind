# Implementation Plan: Upper Decks Rework

## 1. Overview & Objectives

This implementation plan details the sequential validation, tuning, and cross-system integration steps required to take the **`UpperDeckCurves`** system from its current "Holding Place" baseline to a fully verified, production-ready subsystem in the **`By Wood and Wind`** naval architecture pipeline.

### Core Goals:
1. **Geometric Integrity**: Ensure all upper deck segments (Forecastle, Quarterdeck, Gangways, Poop Deck) adhere strictly to inner hull envelopes with zero gaps or clipping.
2. **Safeguard Enforcement**: Validate vertical clearance limits and longitudinal footprint containment under dynamic user interaction.
3. **Cross-System Synchronization**: Confirm seamless interoperation with `MainDeckCurves`, `TopLineCurves`, `SectionModelCurves`, and downstream framing.
4. **Editor Workflow**: Guarantee fluid Scene View manipulation with responsive 3D handles and one-click presets.

---

## 2. Implementation & Verification Phases

```text
┌────────────────────────────────────────────────────────┐
│ Phase 1: Scene & Datum Baseline Verification           │
│ - Validate GameObject setup & serialized references    │
│ - Verify default Tier 1 instantiation & zero-GC buffers│
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Phase 2: Boundary Conformance & Safeguard Validation   │
│ - Test stem/transom flush magnetic snapping            │
│ - Test "No Floating Decks" Tier 2 footprint clamping   │
│ - Test vertical clearance safeguard thresholds         │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Phase 3: Geometry & Visual Fidelity Tuning             │
│ - Proportional camber drop verification                │
│ - Solid dual-surface & edge cap alignment              │
│ - Waist breastwork bulkhead & stanchion grounding      │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Phase 4: Downstream Lofting & Cross-Script Integration │
│ - SectionModelCurves bulwark lofting test              │
│ - Transverse frame upper deck clamp notches            │
│ - Strakes/ribband clearance alignment                  │
└──────────────────────────┬─────────────────────────────┘
                           │
┌──────────────────────────▼─────────────────────────────┐
│ Phase 5: Presets, QoL & Git Deployment                │
│ - Vessel preset configurations (Frigate, Sloop, SOL)   │
│ - Interactive handle sensitivity & UX polish           │
│ - Commit & sync to GitHub origin/main                  │
└────────────────────────────────────────────────────────┘
```

---

## 3. Detailed Phase Breakdown

### Phase 1: Scene & Datum Baseline Verification

| Task ID | Item | Description | Success Criteria |
| :--- | :--- | :--- | :--- |
| **1.1** | **Scene Hierarchy Check** | Verify GameObject `UpperDeckCurves` in `Matched curves.unity`. | Component exists, is active, and has script GUID linked. |
| **1.2** | **Datum References** | Verify linkages to `MainDeckCurves`, `TopLineCurves`, `SheerCurves`, `BreadthCurves`, `FashionCurves`, `BodyCurves`, `FloorCurves`, `BilgeCurves`, `BackboneCurves`. | No `null` reference warnings in Unity Console during `EnsureInitialized()`. |
| **1.3** | **Default Tier Instantiation** | Trigger `Reset to Default Upper Decks` via Inspector context menu. | Generates **Tier 1 (Upper Decks)** with two enabled segments: **Forecastle** and **Quarterdeck**. |
| **1.4** | **Buffer Allocation** | Inspect runtime buffers (`cachedCenterlinePts`, `cachedDeckBeams`, etc.). | Arrays allocate once at `RESOLUTION = 48`; zero memory re-allocation during continuous updates. |

---

### Phase 2: Boundary Conformance & Safeguard Validation

| Task ID | Item | Description | Success Criteria |
| :--- | :--- | :--- | :--- |
| **2.1** | **Stem Flush Snapping** | Drag the Forecastle forward cone handle toward the bow. | Within $0.05\text{m}$ of the inner stem post, handle magnetically snaps flush to stem with zero gap; `flushToStem` toggles to `true`. |
| **2.2** | **Transom Flush Snapping** | Drag the Quarterdeck aft cone handle toward the stern. | Within $0.05\text{m}$ of the inner fashion timbers, handle snaps flush to transom rake; `flushToTransom` toggles to `true`. |
| **2.3** | **One-Click Snap Buttons** | Click `⚡ Snap Forecastle Flush to Stem` and `⚡ Snap Quarterdeck Flush to Transom`. | Instantly locks extremities flush without manual handle dragging. |
| **2.4** | **"No Floating Decks" (Tier 2)** | Click `+ Add Poop Deck (Tier 2)`. Drag the supporting Quarterdeck forward break aft. | Poop Deck forward boundary automatically shrinks/clamps backward so it never hovers over the open waist ($Z_{\text{fwd}}^{\text{Poop}} \le Z_{\text{fwd}}^{\text{QD}}$). |
| **2.5** | **Vertical Space Safeguard** | Lower `TopLineCurves` or raise `MainDeckCurves` until remaining clearance $< 0.68\text{m}$. | Inspector displays warning HelpBox; deck creation is blocked until clearance is restored. |

---

### Phase 3: Geometry & Visual Fidelity Tuning

| Task ID | Item | Description | Success Criteria |
| :--- | :--- | :--- | :--- |
| **3.1** | **Proportional Camber** | Inspect deck beam curvature at midships vs. narrow bow/stern stations. | Parabolic camber crown smoothly tapers in proportion to local hull beam; no excessive lateral dip at narrow extremities. |
| **3.2** | **Dual Solid Planking** | Toggle `showBottomDeck` and adjust `deckThickness` ($0.10\text{m}\text{--}0.30\text{m}$). | Top walking deck and lower ceiling surface render cleanly separated by specified thickness; edge cap lines join both surfaces along the hull. |
| **3.3** | **Waist Breastwork Bulkheads** | Inspect the transverse bulkhead at Forecastle aft break ($Z_{\text{break}}$) and Quarterdeck forward break. | Transverse arcs perfectly match upper deck camber and ground down to the `MainDeckCurves` walking surface. |
| **3.4** | **Breastwork Stanchions** | Adjust `breastworkStanchionCount` ($2\text{--}16$). | Vertical framing stanchions distribute evenly across the bulkhead opening connecting top and bottom arcs. |
| **3.5** | **Bulwark Rails & Framing** | Toggle `showBulwark` on Tier 1 and Tier 2. | Longitudinal cap rails loft cleanly above the upper decks up to the sheer gunwale line, with evenly spaced vertical bulwark stanchions. |

---

### Phase 4: Downstream Lofting & Cross-Script Integration

| Task ID | Item | Description | Success Criteria |
| :--- | :--- | :--- | :--- |
| **4.1** | **`SectionModelCurves` Integration** | Verify calls to `upperDeckCurves.HasBulwarkAtZ(z, ...)` in `SectionModelCurves.cs`. | Transverse hull sections extend upward to upper deck bulwarks where upper decks exist, and drop down to waist bulwarks in the open waist. |
| **4.2** | **Deck Clamp / Shelf Alignment** | Compare upper deck edge points with transverse station frame cuts. | Waterway edges sit on the inner frame face without puncturing outer hull planking. |
| **4.3** | **Waist Gangway Integration** | Click `+ Add Midship Gangway` on Tier 1. | Creates narrow catwalks over the waist connecting Forecastle and Quarterdeck without obstructing centerline waist hatches. |
| **4.4** | **Framing / Strakes Clearance** | Verify clearance between upper deck levels and gunport strake bands in `StrakesModel.cs`. | Upper deck levels align with intended gunport sills (e.g., quarterdeck carronades / forecastle chasers). |

---

### Phase 5: Presets, QoL & Git Deployment

| Task ID | Item | Description | Success Criteria |
| :--- | :--- | :--- | :--- |
| **5.1** | **Frigate Layout Preset** | Standard 2-deck layout: Forecastle ($Z = 14\text{m} \to 8\text{m}$), Open Waist ($Z = 8\text{m} \to -2\text{m}$), Quarterdeck ($Z = -2\text{m} \to -12.5\text{m}$). | Verified visually in Scene view. |
| **5.2** | **Ship-of-the-Line Preset** | Multi-tier layout: Forecastle + Quarterdeck on Tier 1, Poop Deck on Tier 2. | Verified visually in Scene view. |
| **5.3** | **Scene View Interaction Polish** | Test dragging handles in 3D perspective and orthographic views. | Handles move smoothly with Undo/Redo support (`Ctrl+Z`). |
| **5.4** | **Git Version Control** | Stage, commit, and push all verified code, scene updates, and documentation. | Working tree clean, synced to `origin/main` on GitHub. |

---

## 4. Execution Protocol & Verification Checklist

To execute this plan systematically, complete each test and record results:

- [ ] **Step 1**: Open Unity, select GameObject `UpperDeckCurves` in scene `Matched curves`.
- [ ] **Step 2**: Trigger Inspector context menu $\to$ `Reset to Default Upper Decks`.
- [ ] **Step 3**: Verify Scene View gizmos: Orange/amber Forecastle and Quarterdeck, open waist in between.
- [ ] **Step 4**: Test interactive sliders:
  - Drag Forecastle forward handle to bow $\to$ confirm magnetic snap.
  - Drag Quarterdeck aft handle to stern $\to$ confirm magnetic snap.
  - Drag midship elevation handle $\to$ confirm headroom scales smoothly.
- [ ] **Step 5**: Test Tier 2 Poop Deck creation:
  - Click `+ Add Poop Deck (Tier 2)` $\to$ confirm Poop Deck renders atop Quarterdeck.
  - Drag Quarterdeck forward break $\to$ confirm Poop Deck forward boundary clamps cleanly.
- [ ] **Step 6**: Check Unity Console for zero warnings/errors.
- [ ] **Step 7**: Save scene (`Ctrl+S`) and commit changes to GitHub.
