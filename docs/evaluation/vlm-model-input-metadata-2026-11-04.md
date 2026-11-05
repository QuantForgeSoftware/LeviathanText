# Model input metadata and remaining runtime gap

The three local projector packages contain different vision metadata. This makes their
input pipelines worth inspecting before the next geometry experiment, but **does not
establish how LM Studio actually resized, tiled or encoded any recorded image**.

The read-only inventory at
[gguf-metadata-inventory.json](../../.artifacts/vlm-benchmark/vlm-bench-1/root/gguf-metadata-inventory.json)
records offsets, exact metadata-prefix hashes and selected values for all six model/projector
files. Large tokenizer arrays are omitted from the JSON; their exact containing prefixes
remain hashed. The earlier complete-file SHA-256 receipts are referenced, not represented
as newly computed hashes. No tensor data was loaded and no model file was changed.

The bounded inspection utility follows the little-endian v3 metadata layout in the
[official GGUF specification](https://github.com/ggml-org/ggml/blob/master/docs/gguf.md).
It rejects unknown types, invalid booleans, duplicate keys, truncation and excessive metadata,
array or nesting sizes. It is an artifact inspection tool, not a product model loader.

An independent audit passed 916 checks by rereading the recorded prefixes and verifying
field offsets/types/values, projector fields, template hashes and reader rejection bounds.
Evidence: `root/gguf-independent-audit.json`. The metadata limit bounds the prefix size;
the utility rereads that prefix to hash it, so it is not a total-I/O limit.

| Local projector | `clip.projector_type` | `clip.vision.image_size` | `clip.vision.patch_size` | Explicit `clip.vision.spatial_merge_size` |
| --- | --- | ---: | ---: | ---: |
| Qwen3-VL 8B F16 | `qwen3vl_merger` | 768 | 16 | 2 |
| Qwen2.5-VL 7B F16 | `qwen2.5vl_merger` | 560 | 14 | absent |
| Mistral Small 3.1 24B F16 | `pixtral` | 1540 | 14 | 2 |

“Absent” is not zero and does not establish a runtime default. `image_size` is a stored
metadata value, not a demonstrated maximum dimension or proof of a square resize. The
inventory also retains the exact mean/std arrays. These differ between Qwen3 and the other
two packages; no client normalization was introduced from these values.

The model files contain these embedded `tokenizer.chat_template` strings:

| Model | Characters | SHA-256 of UTF-8 template |
| --- | ---: | --- |
| Qwen3-VL 8B | 5,292 | `3636d0f0bd6bef02654cdffdc447b79cb2cef8ab02cc75267345946291a489e4` |
| Qwen2.5-VL 7B | 1,017 | `a0bc6f6fc7a29a80017a433e8f03a1cc1236e838a944a2d034295a60c4f2fddb` |
| Mistral Small 3.1 24B | 2,002 | `391b7940a4191b7667b3cb61a00afa18edf9f07541a30a042d836226e0782e1e` |

The strings are retained exactly in the inventory. They do not prove which effective
template LM Studio selected or how it rendered the submitted system/user messages.
Likewise, `root/runtime-file-receipt.json` pins 18 installed backend files at session end;
it is not a pre-request binary attestation for earlier runs.

## Next engineering check

Before interpreting the [geometry results](vlm-geometry-results-2026-11-04.md) mechanistically,
capture whatever the installed backend exposes about the effective image grid, resize/
tiling decisions, vision-token count and rendered template. Record unavailable fields as
unavailable. The current wire responses did not provide token usage; do not infer image
tokens from the metadata table or label width/height as an observed internal tensor shape.

Keep exact raw, padded and scaled source PNGs and their hashes. Any new backend option must
be named, pinned and tested in a newly registered comparison with the original condition as
control. The present direction differs by model: Qwen3's synthetic aggregate improved with
padding/enlargement, whereas Qwen2.5 and Mistral worsened. These observations justify further
measurement, not a universal resize rule or a claim about handwritten Greek.
