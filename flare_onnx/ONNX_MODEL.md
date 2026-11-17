# `flare.onnx` – interface specification

`flare.onnx` is a single ONNX model (opset 17, ~28 MB), included in this repository via Git LFS. It contains the complete FLARE inference graph:
- the spectrum encoder,
- the molecule encoder (GCN),
- FILIP scoring.

It scores **one spectrum** against a **batch of B candidate molecules**. All preprocessing happens outside the model: peak processing, subformula assignment, and RDKit-compatible atom features. The C# reference implementation is in `src/Flare.Onnx`.

All tensors are `float32`. The dynamic dimensions are:

| Name    | Meaning                                                   |
|---------|-----------------------------------------------------------|
| `peaks` | P = number of formula-annotated peaks of the spectrum (≥ 1) |
| `batch` | B = number of candidate molecules (≥ 1)                   |
| `atoms` | N = number of heavy atoms of the largest candidate in the batch (≥ 1) |

## Inputs

### `spec` — `float32 [P, 15]`

Each row is one peak that has an assigned subformula and m/z ≤ 1000. The model is permutation-invariant over peaks. The original pipeline sorts the peaks by m/z.

| Column | Content |
|--------|---------|
| 0–13   | Element counts of the peak's subformula divided by the normalisation vector below |
| 14     | Peak intensity (sqrt-transformed, relative; precursor = 1.1, see below) |

| Col | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 |
|-----|---|---|---|---|---|---|---|---|---|---|----|----|----|----|
| Element | H | C | O | N | P | S | Cl | F | Br | I | B | As | Si | Se |
| Divisor | 102 | 59 | 25 | 13 | 3 | 6 | 6 | 17 | 4 | 4 | 1 | 1 | 5 | 2 |

Elements outside this list are ignored.

**Example:** subformula `C7H6O2` with intensity 0.42 gives `[6/102, 7/59, 2/25, 0, …, 0, 0.42]`.

How the rows are produced (MIST preprocessing; `SpectrumPreprocessor`, `SubformulaAssigner`, `SpectrumFeaturizer`):

1. Merge peaks whose m/z is equal when rounded to 4 decimals, keeping the max intensity.
2. Drop peaks with m/z > parent mass + 1.
3. Normalise intensities to max = 1, then apply `sqrt`.
4. Keep the 60 most intense peaks with intensity > 0.001.
5. Assign each peak the closest subformula of the neutral precursor formula, plus the adduct ion mass. Only subformulas with RDBE ≥ 0 are candidates. Keep the peak if the mass error is < 20 ppm, with the ppm denominator clipped to ≥ 200. Unassigned peaks are dropped.
6. Precursor peak:
   - If the precursor formula itself was assigned, set its intensity to **1.1**.
   - Otherwise, append a peak (precursor m/z, precursor formula, 1.1).
7. Sort by m/z and drop peaks with m/z > 1000.

### `atoms` — `float32 [B, N, 78]`

Atom features of each candidate, using the dgllife "full" featurisation. Atoms follow the RDKit order from `MolFromSmiles` and include no explicit hydrogens. Rows ≥ the molecule's atom count are zero-padded.

| Offset | Len | Feature | Encoding |
|--------|-----|---------|----------|
| 0  | 1  | atom mass | `GetMass() * 0.01` |
| 1  | 15 | element | one-hot over H, C, O, N, P, S, Cl, F, Br, I, B, As, Si, Se, *unknown* |
| 16 | 4  | bond types at atom | 1 if any bond is SINGLE / DOUBLE / TRIPLE / AROMATIC |
| 20 | 11 | degree | one-hot 0–10 |
| 31 | 6  | total degree (incl. H) | one-hot 0–5 |
| 37 | 6  | explicit valence | one-hot 1–6 |
| 43 | 7  | implicit valence | one-hot 0–6 |
| 50 | 5  | hybridisation | one-hot SP, SP2, SP3, SP3D, SP3D2 |
| 55 | 5  | total number of H | one-hot 0–4 |
| 60 | 5  | formal charge | one-hot −2…+2 |
| 65 | 5  | radical electrons | one-hot 0–4 |
| 70 | 2  | aromatic | one-hot [False, True] |
| 72 | 2  | in ring | one-hot [False, True] |
| 74 | 4  | chiral tag | one-hot UNSPECIFIED, CW, CCW, OTHER |

A value outside an allowed range gives an all-zero one-hot block.

### `adj` — `float32 [B, N, N]`

Dense adjacency matrix including self loops: **A + I**.
- For every bond u–v, set `adj[b, u, v] = adj[b, v, u] = 1`.
- For every real atom i, set `adj[b, i, i] = 1`.
- Padded rows and columns are 0.

The matrix is used unnormalised: a GCN layer computes `adj @ X @ W`. In general the convention is `adj[b, dst, src]`, which is symmetric for molecules.

### `atom_mask` — `float32 [B, N]`

`1.0` for real atoms and `0.0` for padding. Padded atoms are excluded from scoring.

## Outputs

### `scores` — `float32 [B]`

The FILIP similarity of each candidate to the spectrum. A higher score means a better match; rank candidates by descending score. The value range is [−1, 1]; observed values are typically about 0.1–0.6. Scores are comparable only within the same spectrum.

`similarity` is cosine-based, so the score is computed as:

```
peak_side[b] = mean over peaks p  of  max over real atoms n  of similarity[b, p, n]
atom_side[b] = mean over real atoms n  of  max over peaks p  of similarity[b, p, n]
scores[b]    = (peak_side[b] + atom_side[b]) / 2
```

This is identical to `flare.utils.general.filip_similarity_batch(reduction='mean')`.

### `similarity` — `float32 [B, P, N]`

Cosine similarity between each L2-normalised peak embedding and each L2-normalised atom embedding (512-d each), in [−1, 1]. It is useful for peak-to-atom explanations, such as the heatmap in the Streamlit app. Entries for padded atoms (`atom_mask = 0`) are meaningless and must be ignored.

## Notes

- Only one spectrum per call. To score several spectra, call the model once per spectrum.
- Batching candidates of very different sizes wastes compute on padding (O(B·N²) for `adj`). The C# `FlareModel` scores candidates in chunks of 64.
- Supported adducts for step 5:
  - `[M+H]+`, `[M+Na]+`, `[M+K]+`
  - `[M-H2O+H]+`, `[M+H3N+H]+`, `[M]+`, `[M-H4O2+H]+`
  - `[M-H]-`
- Numerical parity with the original PyTorch/DGL model is verified against `tests/Fixtures/reference.json`; the maximum score deviation is < 2e-7.
