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

## 7. Current Implementation Status & Integration

The architecture described above is fully implemented in:
- **`Assets/Scripts/Primary Scripts/UpperDeckCurves.cs`** (2,400 lines)
- **`Assets/Scenes/Matched curves.unity`** (Configured with UpperDeckCurves component)

It seamlessly integrates with:
- `MainDeckCurves.cs` (Floor datum)
- `TopLineCurves.cs` (Ceiling datum & bulwarks)
- `FashionCurves.cs` (Stern transom boundary)
- `SheerCurves.cs` (Stem profile & transom rake)
- `BreadthCurves.cs`, `BilgeCurves.cs`, `FloorCurves.cs`, `BackboneCurves.cs` (Transverse hull breadths)
