# BCM_Toony_shader

Standalone hybrid toon shader for Unity, derived from the Toony Colors Pro 2 – Hybrid Shader.
Self-contained and **portable**: copy this whole folder into any project and it works — no other
asset (including TCP2) needs to be installed.

## What's in this folder
| File | Purpose |
|------|---------|
| `BCM_Toony_shader.shader` | The shader. One SubShader for the **Built-in Render Pipeline**, one for **URP**. |
| `BCM_Toony_Include.cginc` | All the lighting/feature code, shared by both pipelines and every pass. |
| `BCM_Toony_shader_02.shader` + `BCM_Toony_Include_02.cginc` | Second generation. Adds the planar ("blob") ground shadow and the LEGO stud contact shadow. |
| `BCM_Toony_shader_03.shader` + `BCM_Toony_Include_03.cginc` | Third generation. Adds the **X-Ray Overlay** (eyebrows through hair) — see below. |
| `Editor/BCM_ToonyShaderGUI.cs` | Standalone material inspector (`BCM.ToonyShaderGUI`). Optional — if removed, Unity shows the default inspector and the shader still works. |
| `Editor/BCM_ToonySectionClipboard.cs` | Per-section Copy/Paste in the inspector. Holds the table of which properties and keywords belong to each section. Required by the inspector above. |
| `Debug/` | Dev tools for authoring the vertex colour outline mask. Safe to delete before shipping. |

## How to move it to another project
Just copy the `BCM_Toony_shader` folder into that project's `Assets`. That's it.

- **Built-in RP** and **URP** are both supported by the single shader; Unity picks the matching SubShader automatically.
- The URP version is auto-detected from the Unity version (`UNITY_VERSION`), so you do **not** need to regenerate the shader when moving between URP 10 → 17+. Detection is biased conservatively; on a brand-new URP you may miss the very newest optional keywords but it always compiles and renders.
- Shader menu path: **`BCM/BCM_Toony_shader`**.

## Features (match the inspector sections)
Base (Highlight/Shadow color, shadow albedo texture) · Ramp Shading (Default / Crisp / Bands / Bands Crisp / **Texture/Gradient**) · Normal Mapping · Specular (GGX / Stylized / Crisp) · Emission · Rim Lighting · MatCap (+ mask, + **per-region channel texture**) · Global Illumination (Indirect Diffuse, Environment Reflections, Occlusion) · Outline · Options.

## Gradient ramp (the highlighted request)
Set **Ramp Type → Texture** to reveal the **Gradient Ramp Generator** in the inspector:
- Edit a `Gradient` (light → shadow, left to right) and click **Generate & Assign Ramp Texture** —
  it bakes a PNG next to the material and assigns it to the Ramp slot.
- Or assign your own gradient PNG authored in Photoshop directly to the **Ramp Texture** slot.

## Per-region MatCap (Channel Texture)
Turn on **MatCap → Channel Texture** to drive four independent matcaps from the RGBA channels of one
region map. Each channel gets its own Blending / Texture / Color / Power. While it is on, the four base
MatCap settings above are **fully ignored** and greyed out in the inspector; the **Enable Mask** below
the channel blocks is the same shared mask and still gates the whole effect.

Each channel is a **weight, not an index**. That is deliberate: an index map cannot be filtered. Where a
mip level or a bilinear tap blends a red region into a green one, weights cross-fade red into green,
whereas an index map would resolve the blended value to a bogus "yellow" region and draw a fringe along
every border. Weights are normalised (`/ max(1, r+g+b+a)`) so overlapping regions cannot over-brighten,
and a fully black texel means "no matcap here".

Because of that, **combination colours are not extra regions** — magenta is 50 % red + 50 % blue, not a
fifth look. Four regions is the ceiling for one texture; paint the fourth into alpha.

**Enable A Channel** is off by default and must be ticked to use the fourth region. A PNG saved without
an alpha channel reads as `alpha = 1` on *every* texel, so leaving A always-on would paint the A matcap
over the whole mesh and — through the weight normalisation — halve every R/G/B region. Three-channel
region maps therefore work correctly out of the box. The inspector raises an error if the toggle is on
while the assigned texture has no alpha channel.

Replace and Additive contributions accumulate separately and apply once each, so the result never depends
on channel order where regions overlap (two Replace channels average instead of one winning).

### Channel texture import settings — required
The region map carries data, not colour, and Unity's colour-texture defaults corrupt every one of the
weights. The inspector validates the assigned texture and offers a **Fix Import Settings** button:

| Setting | Value | Why |
| --- | --- | --- |
| **sRGB (Color Texture)** | **off** | Gamma-converts the channel values, shifting every region border. |
| **Alpha Source** | **Input Texture Alpha** | Otherwise the A region silently disappears. |
| **Alpha Is Transparency** | **off** | Dilates colour into alpha-0 areas, contaminating the other channels. |
| **Compression** | **None** (or BC7) | Block compression breaks per-channel independence and invents channel values at borders — a magenta fringe along every red/blue edge. |

`Compression` lives in the **platform tabs at the bottom** of the importer (Default / Standalone / Android
/ iOS), not in the top section — and it is **hidden whenever that tab's `Format` is set to anything other
than `Automatic`**, because an explicit format already determines the compression. On a project with
per-platform overrides (e.g. Android forcing `ASTC_6x6`), set that tab's **`Format` to `RGBA 32 bit`**
instead of hunting for the Compression dropdown. The inspector validates every overridden platform, names
the offending one, and its **Fix Import Settings** button rewrites the overrides too.

Watch the per-platform **Max Size** as well: an override that clamps a 1024 map to 512 is not a
correctness bug (a downscaled weight map still blends legally) but it does soften region borders.

Mip maps and bilinear filtering should stay **on**: with weight semantics they are what makes the borders
smooth. To soften borders further, blur the source texture 1–2 px rather than adding a shader parameter.

Not supported in **Mobile** mode — the channel path is compiled out and the base MatCap is used instead.
The inspector warns when the material is in that mode.

## Copying settings between materials
Every inspector section has **Copy** / **Paste** buttons in its header, plus **Copy All / Paste All** at
the top. Copy grabs that section's property values *and* its keywords. Paste applies to every selected
material at once and is undoable. Copy is disabled while more than one material is selected, since the
source would be ambiguous.

The payload is JSON written to both the editor session and the OS clipboard, so it can be pasted into
another Unity instance or shared as text. Textures are stored as asset GUIDs — pasting into a different
project leaves texture slots untouched (and logs which ones) rather than clearing them.

## Specular albedo tint
The specular colour is a single uniform colour, which looks wrong on a multi-coloured albedo — a red
decal on a blue box would get a blue highlight. **Specular → Albedo Tint** mixes `_SpecularColor`
toward the surface colour per pixel, so each region gets a highlight in its own colour. No extra
texture and no extra sampler: the albedo is already sampled.

- `0` (default) — uniform specular colour, identical to the original behaviour.
- `1` — the highlight fully takes the albedo colour.
- **Preserve Brightness** — take only the hue, so a dark albedo still gets a full-strength highlight.
  Pure black falls back to a white highlight instead of losing it entirely.

The tint reads the albedo *before* the shadow-albedo texture, matcap and decal blends are applied.
Those are lighting dependent, so using the blended albedo would make the highlight colour drift as
the light moves and bleed the matcap colour into the highlight.

## Vertex colour outline mask
Per-vertex control over where the outline appears. Turn on **Outline → Vertex Color Mask** and pick a
channel; that channel's value multiplies the outline width, so **vertices painted black get no outline**
and white get the full width. `Invert Mask` covers meshes painted the other way round.

Art pipeline requirements:
- Paint in **RGB**, not alpha. 3ds Max's VertexPaint does not carry alpha through FBX export — it
  arrives as a uniform 1.0.
- For a crisp cut, the boundary must fall on **split vertices** (separate smoothing group / hard edge).
  A triangle whose vertices have mixed mask values tapers instead of cutting.
- Using RGB for the mask rules out **Outline Normals Source → Vertex Colors**, which reads the same
  channels as smoothed normals. Use `Tangents` or `UV1`–`UV4` instead.

Two tools in `Debug/` help author this:
- Shader **`BCM/Debug/Vertex Color`** — view any channel, or **Outline Mask Preview** (red = no outline,
  green = outline; a yellow band marks a boundary that will taper).
- **Tools > BCM > Inspect Vertex Colors** — reads the mesh data and reports per-channel range and
  distribution. Use this to tell "painted white" apart from "no vertex colour data", which look
  identical to any shader.

## X-Ray Overlay — eyebrows through hair (`BCM_Toony_shader_03`)

The stylised-anime trick where eyebrows and eyes stay readable under a heavy fringe. It is **not** a
shading effect: those fragments are killed by the depth test long before any lighting runs, so the
whole feature is render state. That is also why it costs exactly **one** shader keyword (`BCM_XRAY`,
for the optional depth push) — `ZTest` and the stencil ops are driven straight from material
properties, which Unity does not turn into shader variants.

**Requirement on the model:** the eyebrow has to be its own submesh with its own material, floating on
(or just inside) the face — the same way HoYo-style heads are built. The whole material draws over the
hair, so an eyebrow painted into the face texture cannot be separated out this way.

### Setup
| Material | Role | What the role sets |
|---|---|---|
| eyebrow / eye submesh | **Overlay** | `ZTest Always`, Depth Write off, queue **2460** |
| face | **Mask** | stamps stencil bit 32, queue **1990** |
| hands, props that may cover the face | **Blocker** | clears stencil bit 32 |
| everything else, hair included | **Off** | never touches the stencil buffer |

Set **Overlay** on the eyebrow and the effect is already there. Then turn on **Restrict To Face Mask**
and set the face to **Mask** — without it the eyebrow also draws through anything else in front of the
face, a hand included.

### The part that is easy to get wrong
The **Mask** must be drawn *before* the hair, which is why its role forces queue 1990. A stencil write
only lands where the *depth* test passed too; if the face shares the hair's queue it is drawn second
(opaque geometry sorts front-to-back and the hair is nearer), so it loses the depth test under the
fringe and stamps nothing exactly where the mask was needed. At a shared queue the overlay vanishes
completely. The inspector warns when the queue drifts off the expected value.

**Blocker** exists because the Mask role is *nearly* enough on its own — a hand drawn before the face
already blocks the face's stencil write — but opaque order inside one queue is sorted per renderer and
is not a guarantee. Use Blocker on anything that must reliably hide the face.

### Stencil bits
Bit 5 (**32**). URP reserves the low nibble for its own material-type and light masks and leaves bits
4–7 to the user; the planar shadow pass in `_02`/`_03` owns bit 7 (**128**), so 16 / 32 / 64 are free.
In `_03` the planar shadow's stencil block is masked down to bit 7 — in `_02` it was a bare `Ref 42`,
which wrote bits 1/3/5 and compared against all eight.

### Depth Push (alternative, no stencil)
Pulls the rasterised position toward the camera so a thin mesh wins an ordinary `LEqual` depth test —
no stencil setup, no queue change. Also the standard fix for an eyebrow modelled flush on the face
z-fighting with it. Only the rasterised position moves; lighting, shadows and fog are still evaluated
where the mesh actually is. Read off the view matrix rather than from the camera position, so it also
works under an **orthographic** camera. Push further than the hair is thick and the overlay pokes out
through the nose instead.

### Gotcha: URP Depth Priming
Leave **Depth Priming Mode = Disabled** on the URP renderer (it is disabled in this project). Priming
makes the opaque pass depth-test `Equal` against a prepass via a render state override, which
overrides every material's `ZTest` — including the overlay's `Always`, killing the effect.

Hair on a **Fade/Transparent** material (queue 3000) draws after the overlay and covers it. Give that
hair a **Cutout** material instead.

## Notes / differences vs the original
- The outline is built into this single shader (no separate outline shader). The **Enable Outline**
  toggle drives the `BCM_OUTLINE` keyword; when off, the outline pass renders nothing.
- The `TCP2_*` keyword names are kept internally so the ported lighting code is unchanged and behaves
  identically to the original hybrid shader.
