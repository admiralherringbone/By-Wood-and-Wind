# UpperDeckCurves Architecture & Technical Specification

## 1. Executive Summary & Design Philosophy

In classical wooden ship construction (such as 17th–19th century naval architecture), the upper works of a ship do not run as a single continuous flat surface from stem to stern. Instead, they are characterized by:
- A continuous **Main Deck (Gun Deck / Upper Deck)** that forms the structural backbone of the hull.
- Raised **Upper Decks (Forecastle and Quarterdeck)** situated at the bow and stern.
- An **Open Waist** amidships where the main deck is exposed to the elements.
- Optional elevated superstructures (**Poop Deck** aft, **Topgallant Forecastle** forward).

The **Upper Decks Rework** in `UpperDeckCurves.cs` transforms the upper hull modeling from an ad-hoc or unconstrained curve approach into a **rigorously bounded, multi-tier vertical corridor system**. It ensures that every upper deck segment:
1. Conforms laterally to the exact 3D inner hull profile (lofting between Keel, Floor, Bilge, Breadth, and TopLine).
2. Snaps seamlessly against the forward inner stem post and aft inner transom timbers without gaps.
3. Respects physical vertical clearance and structural footprint containment so higher tiers never float in mid-air.

---

## 2. The Vertical Corridor Architecture

The ship hull is organized into distinct vertical layers bounded by datum curves:

```text
▲ SUPERSTRUCTURE (Tier 2): Poop Deck, Topgallant Forecastle
│   - Strictly supported by underlying Tier 1 deck footprints
│   - Elevation = Lower Deck Surface + Tier Headroom
├── CEILING BOUNDARY: TopLineCurves (Sheer Rail / Gunwale)
│
│   ┌─────────────────────────────────────────────────────────────────┐
│   │ UPPER DECKS (Tier 1): Forecastle, Quarterdeck, Midship Gangways │
│   │ - Bounded between Main Deck Top Surface and TopLineCurves       │
│   │ - Headroom defaults to 2.05m above Main Deck                     │
│   │ - Natural Bulwark Height = Y_TopLine(z) - Y_UpperDeck(z)        │
│   └─────────────────────────────────────────────────────────────────┘
│
├── PRIMARY DATUM: MainDeckCurves (Continuous Floor Datum)
│   - Spans stem-to-stern; acts as ceiling for lower decks & floor for upper decks
│
│   ┌─────────────────────────────────────────────────────────────────┐
│   │ LOWER DECKS (Tier 0-): Gun Deck, Berth Deck, Orlop, Hold        │
│   │ - Bounded between Main Deck Bottom Surface and Inner Keel       │
│   └─────────────────────────────────────────────────────────────────┘
│
▼ FLOOR DATUM: Keelson / Inner Keel
```

### Why This Mathematical Model Is Superior
1. **Natural Bulwark Generation**:
   Rather than manually drawing arbitrary bulwark rails, the bulwark height for Tier 1 upper decks is derived directly from the hull ceiling:
   $$\text{Bulwark Height}(z) = Y_{\text{TopLine}}(z) - Y_{\text{UpperDeck}}(z)$$
   Where the deck is raised (Quarterdeck/Forecastle), the bulwark is lower ($\approx 1.0\text{--}1.2\text{m}$). Where the deck is omitted (the open waist), the bulwark is full height from Main Deck up to TopLine ($\approx 2.5\text{--}3.2\text{m}$).
2. **Zero-Extrapolation Inner Lofting**:
   Because Tier 1 sits between the Main Deck and TopLine, the deck’s waterway edges sit directly on the established inner hull tumblehome/flare.
3. **Open Waist Modeling**:
   The waist is created naturally by leaving a gap between the Forecastle aft boundary ($Z_{\text{aft}}$) and the Quarterdeck forward boundary ($Z_{\text{fwd}}$).

---

## 3. Data Structures: Tiers and Segments

The system uses a two-level hierarchy (`UpperDeckTier` $\to$ `UpperDeckSegment`):

### `UpperDeckTier` (Tier Level Master)
Represents a vertical elevation tier:
- **`tierLevel`**: `1` (Upper Decks directly above Main Deck) or `2` (Superstructures resting atop Tier 1).
- **`tierHeadroom`**: Master headroom in meters (e.g., $2.05\text{m}$).
- **`snapToCeilingBoundary`**: Optional toggle to pin the deck directly to `TopLineCurves`.
- **`sheerScale`**: Curvature ratio of Main Deck sheer applied to this tier (typically $0.80$, softening the sheer curve at higher elevations).
- **`midshipCamber` & `proportionalCamberToBeam`**: Round-of-beam parabolic crown scaled to local hull beam.
- **`segments`**: List of independent deck segments on this tier.

### `UpperDeckSegment` (Physical Deck Instance)
Represents a discrete deck polygon (e.g. "Forecastle", "Quarterdeck", "Poop Deck"):
- **Longitudinal Range**: `forwardZ` to `aftZ`.
- **Boundary Flush Snapping**: `flushToStem` and `flushToTransom`.
- **Superstructure Support**: `supportingSegmentIndex` (links a Tier 2 segment to its supporting Tier 1 deck).
- **Solid Thickness**: `deckThickness` (e.g., $0.18\text{m}$) and `showBottomDeck` (creates the dual top-planking and bottom-ceiling surfaces).
- **Waist Breastworks**: `showForwardBreastwork`, `showAftBreastwork`, and `breastworkStanchionCount`.
- **Cached Buffers**: Zero-GC runtime arrays for centerline, port/starboard edges, deck beams, end arcs, and breastwork stanchions.

---

## 4. Mathematical & Geometric Mechanics

### 1. Centerline Sheer Elevation $Y(z)$
The centerline deck height at station $z$ is evaluated via:
$$Y_{\text{center}}(z) = Y_{\text{midFloor}} + \text{Headroom} + (Y_{\text{MainDeck}}(z) - Y_{\text{MainDeckMid}}) \times \text{sheerScale}$$
- For Tier 1, $Y_{\text{midFloor}}$ is the midship walking surface of `MainDeckCurves`.
- For Tier 2, $Y_{\text{midFloor}}$ is the walking surface of the assigned Tier 1 supporting segment.
- `sheerScale` dampens or amplifies the longitudinal curve relative to the ship's main sheer.

### 2. Proportional Camber (Round of Beam)
Decks crown upward at the centerline and slope outward toward the waterways to shed water:
$$\text{Camber}(z) = \text{Camber}_{\text{mid}} \times \frac{\text{LocalBeam}(z)}{\text{MidshipBeam}}$$
$$\Delta Y(x, z) = -\text{Camber}(z) \times \left(\frac{x}{\text{HalfBreadth}(z)}\right)^2$$
Because beam narrows at the bow and stern, proportional camber prevents excessive parabolic drop at narrow stations.

### 3. Lateral Inner Hull Conformance (`GetInnerHullHalfBreadthAt`)
To ensure deck edges align with the inner frame faces at any elevation $Y$ and station $Z$, the evaluator performs multi-stage vertical interpolation through the ship's established lines:
1. **Keel/Keelson**: Inner keel half-width at $Y_{\text{keelBottom}}$.
2. **Inner Floor**: Interpolates to `floorCurves.GetInnerFloorPointAtZ(z)`.
3. **Inner Bilge**: Interpolates to `bilgeCurves.GetInnerBilgePointAtZ(z)`.
4. **Inner Breadth**: Interpolates to `breadthCurves.GetInnerBreadthPointAtZ(z)`.
5. **Inner TopLine**: Interpolates to `topLineCurves.GetInnerTopLinePointAtZ(z)`.
6. **Topsides Extrapolation**: Above TopLine (for superstructures), projects along the tumblehome/flare tangent slope:
   $$\text{Slope} = \frac{X_{\text{TopLine}} - X_{\text{Breadth}}}{Y_{\text{TopLine}} - Y_{\text{Breadth}}}$$
7. **Transom Cut**: For aft stations passing through the counter/transom, intersects with `fashionCurves.GetInnerFashionPointAtZ`.

### 4. Boundary Flush Snapping
- **Bow Inner Stem Post (`GetInnerStemZAtElevation`)**:
  Samples `topLineCurves` and `breadthCurves` inner tips at elevation $Y$. When `flushToStem` is active, the Forecastle's forward boundary snaps to the inner face of the stem post with zero gap.
- **Stern Inner Transom (`GetInnerSternZAtElevation`)**:
  Samples `fashionCurves.GetInnerFashionPointAtY` and projects along the stern transom rake angle. When `flushToTransom` is active, the Quarterdeck or Poop Deck aft boundary snaps flush against the fashion timber counter.

### 5. Solid Physical Thickness & Breastwork Closures
- **Dual Surface**: Evaluates both the top deck surface ($Y$) and bottom ceiling surface ($Y - \text{deckThickness}$), connected by vertical thickness edge caps.
- **Waist Breastworks**: At transverse breaks facing the open waist, the script generates a 3D bulkhead arc spanning from the upper deck surface down to the Main Deck walking surface, framed by vertical timber stanchions.

---

## 5. Architectural Safeguards

The rework introduces two vital structural constraints:

### 1. Vertical Headroom Safeguard
- **`GetRemainingUpperSpace(forwardZ, aftZ)`**: Samples vertical clearance between Main Deck and TopLine across the segment:
  $$\text{Clearance}(z) = Y_{\text{TopLine}}(z) - Y_{\text{MainDeck}}(z)$$
- **`CanAddUpperDeck()`**: Requires at least $\text{deckThickness} + 0.50\text{m}$ headroom ($\ge 0.68\text{m}$). If TopLine is too low, the Inspector displays a warning and blocks segment creation until geometry is adjusted.

### 2. "No Floating Decks" Containment Safeguard
- Tier 2 superstructures (e.g. Poop Deck) must reference a supporting Tier 1 segment (Quarterdeck).
- **`ClampSegmentBoundaries()`** enforces:
  $$Z_{\text{fwd}}^{\text{Tier 2}} \le Z_{\text{fwd}}^{\text{Tier 1}}, \quad Z_{\text{aft}}^{\text{Tier 2}} \ge Z_{\text{aft}}^{\text{Tier 1}}$$
- If the user resizes the Quarterdeck forward break (e.g. making the waist larger), the Poop Deck automatically clamps backward to remain securely supported atop the Quarterdeck.

---

## 6. Scene View & Inspector Workflow

The system provides immediate visual feedback and interactive editing:
- **Interactive 3D Cone Sliders**:
  - Forward $Z$ handle: Drag forward/aft to resize the deck. If dragged within $0.05\text{m}$ of the stem, it magnetically snaps flush.
  - Aft $Z$ handle: Drag to adjust the aft limit. If dragged near the stern transom, it magnetically snaps flush.
  - Midship Dot handle: Drag vertically to adjust headroom directly in the Scene View.
- **Inspector Quick Presets**:
  - `+ Add Forecastle (Bow)`
  - `+ Add Quarterdeck (Aft)`
  - `+ Add Midship Gangway`
  - `+ Add Poop Deck (Tier 2)` (smartly disabled if no supporting Quarterdeck exists)
  - `⚡ Snap Flush to Stem / Transom` instant action buttons.
- **Live Readouts**: Displays actual headroom ($m$), bulwark height ($m$), and midship beam width ($m$).

---

---

## 7. Classical Naval Draughting Architecture (TopLine Dynamic Crown & Solid/Dashed Representation)

Following classical 18th-century naval draughting principles, upper decks and bulwarks follow strict mathematical and graphical conventions:

### 1. TopLineCurves as the Supreme Dynamic Boundary
- **Supreme Rail**: `TopLineCurves` is always the uppermost sheer rail of the vessel running from stem to stern.
- **Dynamic Elevation (`SynchronizeTopLineElevation`)**: When higher-tier superstructures are present (such as a Poop Deck at Tier 2 or Quarterdeck at Tier 1), `UpperDeckCurves` evaluates the supreme required cap rail height across the entire vessel and synchronizes all key datums in unison:
  1. **Midship Apex (`TopLineCurves.fwdP3.y`)**: Evaluated as the cumulative deck floor elevation up to the highest active tier plus supreme bulwark height ($Y_{\text{mainDeckMid}} + \sum \text{headroom}_t + \text{bulwark}_{\text{supreme}}$). Both apex tangent handles (`fwdP2.y`, `aftP1.y`) lock to `fwdP3.y` ensuring C1 tangent continuity.
  2. **BodyCurves Master Section Top ($P_4$)**: Because `BodyCurves` midship section point $P_4$ samples directly from `TopLineCurves.GetTopLinePointAtZ(targetApexZ)` (which is `fwdP3`), raising `fwdP3.y` immediately elevates $P_4$. Master frame sections across the ship re-loft smoothly from Keel ($P_0$) all the way up to supreme TopLine ($P_4$).
  3. **Stern Transom (`SheerCurves.transomRakeHeight`)**: Evaluates the highest stern cap rail across all active tiers at the transom (or sheers up from midship apex), updating `sheerCurves.transomP1.y` and `topLineCurves.aftP2.y`.
  4. **Bow Stem (`SheerCurves.targetUpperStemTopY`)**: Evaluates the highest bow cap rail across all active tiers at the stem (or sheers up from midship apex), updating `sheerCurves.upperStemTop.y` and `topLineCurves.fwdP0.y` so that the sheer line rises naturally without clamp suppression.
- When upper decks are disabled or removed, all parameters smoothly revert to their baseline Main Deck datums (`baseTopLineMidshipY`, `baseTransomRakeHeight`, `baseUpperStemRatio`).

### 2. Solid vs. Dashed Draughting Convention
- In historical sheer draughts, continuous boundary curves are drawn **solid** where physical structure exists and **dashed** where open to the air:
  - **Over Active Upper Decks (Forecastle, Poop Deck)**: `TopLineCurves` is drawn as a **solid line** representing the physical bulwark cap rail.
  - **Over Open Spans (Open Waist, lower deck gaps)**: `TopLineCurves` is drawn as a **dashed line** representing the continuing sheer profile through open air.
- Evaluated via `upperDeckCurves.IsHighestPhysicalDeckAtZ(zMid)`: TopLine curves and inner offsets render solid where physical structure exists, and dashed (`Vector3.Lerp(p0, p1, 0.5f)`) where open to the air.
- In `SectionModelCurves`, transverse frame cuts at all stations loft continuously from Keel ($P_0$) to TopLine Curves ($P_4$), mirroring the master `BodyCurves` section across the entire ship. Intermediate upper decks, waterways, and waist bulwarks sit cleanly between Breadth ($P_3$) and TopLine ($P_4$) as internal features, leaving the harmonic framing skeleton pure and unbroken.

### 3. Master Frame Invariant on `BodyCurves`
- On `BodyCurves`, only **two curve-modifying control points** exist in the upper hull: $P_3$ (Breadth / Maximum Beam) and $P_4$ (TopLine / Rail).
- With `TopLineCurves.fwdP3.y` raised to the highest upper deck, $P_4$ crowns the supreme deck level (e.g. $8.60\text{m}$ for Poop Deck).
- All intermediate decks (Tier 1 Quarterdeck floor at $5.55\text{m}$, Quarterdeck bulwark at $6.65\text{m}$, Main Deck waist bulwark at $4.60\text{m}$, Main Deck floor at $3.50\text{m}$) sit cleanly **between $P_3$ ($\sim 3.0\text{m}$) and $P_4$ ($8.60\text{m}$)**.
- Every intermediate deck and bulwark is a non-modifying sampling point evaluated along the fair frame arc, preserving harmonic hull lofting without introducing artificial kinks.

### 4. Independent Main Deck Waist Bulwark
- When upper decks create an open waist between them (e.g., between Forecastle and Quarterdeck), the crew on the Main Deck requires an independent bulwark for safety.
- **`waistBulwarkHeight`**: Fully independent height adjustment ($0.40\text{m}\text{--}2.50\text{m}$, default $1.10\text{m}$), completely decoupled from Forecastle or Quarterdeck sheer and headroom.
- **Visuals & Framing**: Renders solid cap rail, inner rail, and vertical framing stanchions (`waistStanchionCount`) across the waist span.
- **Interactive Scene Handle**: An interactive 3D slider handle at midship allows immediate clicking and dragging of waist bulwark height directly in the Scene View.
- **Downstream Conformance**: Positioned cleanly along the inner face of the hull framing between Main Deck and TopLine, providing safety railings without disrupting continuous frame lofting.

### 5. Seamless Intermediate Bulwark Rail Continuity & Knot Snapping
- **Unified Sheer & Camber Formulation**:
  Intermediate tier bulwark rails evaluate along a single, smooth mathematical sheer and camber surface across the entire length of the vessel:
  $$Y_{\text{edge}}(z) = Y_{\text{centerlineSheer}}(z) - \text{camber}(z)$$
  $$Y_{\text{rail}}(z) = Y_{\text{edge}}(z) + \text{tier.bulwarkHeight}$$
  By evaluating this continuous formulation across both active segments and open spans, the rail avoids dropping camber or resetting sheer scaling at transitions, achieving $0.00\text{m}$ vertical ($\Delta Y$) and transverse ($\Delta X$) displacement between solid and dashed segments.
- **Exact Boundary Knot Snapping**:
  Segment transition coordinates (`seg.forwardZ`, `seg.aftZ`) are explicitly injected into the longitudinal knot polyline (`cachedTierZKnots`) and sorted descending. This ensures the solid rail terminates precisely at the breastwork bulkhead rather than slanting across discrete grid steps.
- **Binary Search Polyline Interpolation**:
  `GetBulwarkRailPointsAtZ()` utilizes a binary search across `cachedBulwarkRailPts` to locate the enclosing interval `[i0, i1]`, ensuring exact piecewise-linear interpolation regardless of non-uniform knot spacing.

### 6. Exact BodyCurves Spline Alignment for Outer & Inner Rails
- **The Discrepancy Eliminated**:
  Previously, upper deck bulwark rails sampled `GetInnerHullHalfBreadthAt()` (which used a 2-point linear interpolation between inner Breadth and inner TopLine), mistakenly positioning outer rails on the inner frame face and offsetting inner rails an extra distance into empty space.
- **Exact Outer Hull Evaluation**:
  `BodyCurves.GetOuterHalfBreadthAtY(y, z)` solves the station's cubic Bezier segment between Breadth ($P_3$) and TopLine ($P_4$) via 14-iteration bisection to locate the exact parameter $u$ matching elevation $y$. Outer rails evaluate `GetOuterHullHalfBreadthAt(railY, z)`, locking them 100% to the outer frame lofting curve of `BodyCurves`.
- **Exact Inner Hull Evaluation**:
  `BodyCurves.GetInnerHalfBreadthAtY(y, z)` evaluates the 2D inward frame normal $\hat{n}_{2D} = (-t_y, t_x)$ and frame spacing at elevation $y$. Inner rails evaluate `GetInnerHullHalfBreadthAt(railY, z)`, locking them 100% to the inner frame curve.
- **Deck Waterways**:
  Deck planking edges (`cachedStbdEdgePts`) evaluate `GetInnerHullHalfBreadthAt(edgeY, z)` so walking decks sit flush against the inner face of the hull framing.

### 7. Elevation-Aware Stern Overhang & Theoretical Breadth Projection
- **The Classical Upper Mould Overhang**:
  Aft of the stern post ($Z \approx -11.94\text{m}$) and wing transom, the lower hull (floor and bilge) terminates. The ship extends aft across the counter and transom rake overhang (down to $Z \approx -13.40\text{m}$) consisting purely of the **Upper Mould** (Height of Breadth up to TopLine / Upper Decks).
- **Elevation-Aware Stern Boundary**:
  `BodyCurves.GetOuterHalfBreadthAtY` and `GetInnerHalfBreadthAtY` evaluate `sheerCurves.GetSternProfileZAtY(y)` to determine the aft termination of the hull at elevation $y$. Stations at or aft of the raked transom boundary return the exact fashion timber half-breadth from `FashionCurves.GetFashionPointAtY(y, 1f)` and `GetInnerFashionPointAtY(y, 1f)`.
- **Continuous Theoretical Breadth Sampling**:
  In `BodyCurves.GetSectionNodesAtZ`, `hobX` and `hobY` sample `breadthCurves.GetTheoreticalBreadthPointAtZ(z)` along the fair aft projection line. At stations aft of the stern post ($z \le \text{sternP3.z}$), only `floorX` and `bilgeX` collapse into the deadwood ($hw$), while `hobX` and `topLineX` maintain their fair beam, preventing upper deck waterways and rails from pinching.

---

## 8. Current Implementation Status & Integration

The architecture described above is fully implemented and active in:
- **`Assets/Scripts/Primary Scripts/UpperDeckCurves.cs`**
- **`Assets/Scripts/Primary Scripts/TopLineCurves.cs`**
- **`Assets/Scripts/Primary Scripts/BodyCurves.cs`**
- **`Assets/Scripts/Primary Scripts/SectionModelCurves.cs`**
- **`Assets/Scenes/Matched curves.unity`** (Configured with UpperDeckCurves component)

It seamlessly integrates with:
- `MainDeckCurves.cs` (Floor datum)
- `TopLineCurves.cs` (Ceiling datum & bulwarks)
- `FashionCurves.cs` (Stern transom boundary)
- `SheerCurves.cs` (Stem profile & transom rake)
- `BodyCurves.cs` (Master frame lofting between Breadth and TopLine)
- `SectionModelCurves.cs` (Framing cuts, strakes, and deck clamp shelf notches)
- `BreadthCurves.cs`, `BilgeCurves.cs`, `FloorCurves.cs`, `BackboneCurves.cs` (Transverse hull breadths)
