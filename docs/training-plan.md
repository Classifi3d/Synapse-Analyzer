# Fine-tuning plan for Synapse Analyzer

Review of the original plan, and a revised one for an **RX 7700 XT (12 GB, gfx1101)**
with 32 GB system RAM.

---

## 1. Verdict on the original plan

The architecture is sound. Three decisions in it are the right ones and should not
change:

| Decision | Why it holds up |
| --- | --- |
| Zeek preprocessing to beat the context limit | Already built and verified. A 1,100-connection capture reduces to a ~37 KB prompt. |
| Train on the **exact JSON the app produces at inference** | The single most important choice in the document. Train/serve skew is what usually sinks projects like this. |
| LoRA/QLoRA + PEFT, merge → GGUF → Ollama | Correct method and correct deployment path. The LoRA maths in §9.1 is right. |

Seven things need revising. They are listed in §5.

---

## 2. The measurement that changes the plan

Using the real `AnalysisPromptBuilder` against a real Zeek run of a synthetic capture
(1,100 connections, 300 DNS lookups, 4 of 8 logs populated):

```
Prompt: 36,963 chars  ≈ 9,240 tokens     (at Analysis:PromptRowsPerLog = 40)
```

This is the number the original plan is missing, and it invalidates its
hyperparameters. §9.2 specifies **batch size 4**. At ~9.2k tokens per example that is
~37k tokens in a single forward/backward pass. On 12 GB of VRAM that is not possible for
an 8B model, and it is uncomfortable even for a 4B.

Activation memory scales with sequence length, and sequence length here is set by the
data, not by preference. **Sequence length is the binding constraint on this project,
not parameter count.**

It also already caused a live bug: `Ollama:ContextLength` was 8192, so real prompts were
silently truncated from the front — dropping the system instructions and the `VERDICT:`
rule. The model then produced a shapeless report with no verdict line. Raised to 32768.

---

## 3. Hardware and toolchain

The original plan is implicitly CUDA-shaped. On AMD:

- **The GPU is fine.** gfx1101 (RX 7700 XT) is officially supported in current ROCm on
  Ubuntu 22.04 / 24.04. No `HSA_OVERRIDE_GFX_VERSION` workaround needed.
- **Classic QLoRA is not fine.** 4-bit NF4 comes from `bitsandbytes`, whose ROCm backend
  is still a beta multi-backend branch requiring a source build. Workable, but a poor
  thing to have on the critical path of a deadline.
- **Use Unsloth instead.** It shipped official AMD support covering the RX 7000 series,
  with ROCm-ported Triton kernels: roughly 2× faster training and materially lower VRAM,
  which is exactly the axis that is tight here.

**Run Linux for this.** ROCm on Windows is far more limited.

---

## 4. Model choice

Recommendation: **`Qwen3-4B-Instruct-2507`**.

| Criterion | Why it decides the choice |
| --- | --- |
| Context | 262k native. Prompts are ~9k today and grow with `PromptRowsPerLog`. Nothing here should ever be context-bound again. |
| `Instruct`, not `Thinking` | The Thinking variants emit `<think>` blocks. Those would be stored in `analysis.Report` and render straight into the UI, and could displace the required trailing `VERDICT:` line. |
| 4B at 12 GB | Leaves headroom to train at 8–16k sequence length, which is the actual constraint. |
| Instruction following | The task is format adherence over supplied evidence, which is what this class of model is tuned for. |

Alternatives, in order:

- **`Qwen3-8B`** — better reasoning, and viable with Unsloth + 4-bit, but tight at 9k
  sequence length on 12 GB. Consider only after a 4B run works end to end.
- **`Llama-3.1-8B-Instruct`** — 128k context, the currently configured default.

**Not DeepSeek Coder.** It is a code model — completion and fill-in-the-middle. This
task is prose reasoning over telemetry with a rigid output shape. Code models are
unreliable at holding a five-section markdown structure and one exact trailing line.

You train the model you serve: the adapter is tied to its base. Pick one and commit.

---

## 5. What to change in the original document

1. **§9.2 hyperparameters.** `per_device_train_batch_size: 1`, `gradient_accumulation_steps:
   8–16`, `max_seq_length: 8192–16384`, gradient checkpointing on. Effective batch stays
   at 8–16; real batch cannot exceed 1 at this sequence length.
2. **§9.2 LoRA targets.** "Query and value projection layers" is the 2021 paper's
   setting. Current practice targets every linear layer — `q,k,v,o,gate,up,down`. This
   is a larger quality win than tuning rank, for the same memory.
3. **§6.2 class balancing.** The reasoning is classifier reasoning. This model does not
   emit a class, it writes a report, and the prompt explicitly instructs it to *say so
   plainly when traffic is benign*. Downsampling benign trains it to cry wolf — the
   precise failure a security tool cannot afford. Balance for **scenario diversity**, and
   keep benign strongly represented.
4. **The training unit is undefined.** Dataset labels are per-flow; the app's input is a
   whole-capture summary. These do not match. Define the unit as a **time window**:
   slice each pcap into fixed windows (5 minutes is a reasonable start), run each window
   through the Zeek pipeline, and label the window by the attacks it contains. One
   window → one JSON input → one report. This is what "train on the inference format"
   actually requires.
5. **§6.2 "expert-written response".** Does not scale past a few dozen examples. Use a
   large teacher model to draft each report from the window's Zeek JSON *plus its ground
   truth labels*, then human-review a sample. Distillation, not authorship.
6. **§9.2 evaluation.** F1 alone is insufficient for a generative task. Track three:
   - **Verdict F1** — map `NONE` → benign, everything else → malicious.
   - **Format compliance** — does it emit the `VERDICT:` line and all five sections?
   - **Hallucination rate** — do IPs/domains in the report actually appear in the input?
     This is the metric that matters most for trust, and the prompt already forbids
     invention.
7. **MACCDC is unlabelled.** Excellent for realism and qualitative evaluation. It cannot
   be supervised training data without labels.

Also missing: **catastrophic forgetting**. Narrow fine-tuning degrades general
instruction-following — the very capability the five-section format depends on. Mitigate
with few epochs (1–2), a low learning rate, and a slice of general instruction data mixed
in. Watch format compliance across checkpoints; if it falls, stop.

---

## 6. Does it need fine-tuning at all?

Worth answering honestly before spending a week on it.

A **stock, untuned `llama3.2:3b`** — 2 GB, no training whatsoever — produced this from
the real pipeline:

```
# Executive Summary / # Findings / # Indicators of Compromise
# Risk Assessment / # Recommendations
VERDICT: LOW - Further investigation is warranted due to an unknown DNS query.

all five sections present · verdict parsed correctly · no hallucinated hosts
```

It even flagged a planted suspicious domain. The prompt is doing most of the work, and
the one real failure was **context truncation, not model capability**.

So fine-tuning here is an improvement, not an enabler. Its genuine value is:

- **Verdict calibration** — consistent severity for comparable traffic, which stock
  models are erratic about.
- **House style** — reliable section structure without a long preamble.
- **Reduced hallucination** — grounding answers harder in the supplied rows.

Those are real and defensible. Just sequence it correctly: **baseline first**, so you can
prove the tuning helped. That is both better engineering and a much stronger claim to
write up.

---

## 7. The plan

### Phase 0 — Baseline (do this before any training)

Serve `Qwen3-4B-Instruct-2507` in Ollama, run 30–50 captures through the real app, and
score them on the three metrics in §5.6. This is your control group. Without it, no
statement about the fine-tune's effect is defensible.

### Phase 1 — Data pipeline

1. Acquire pcaps. Pre-labelled PCAP releases of UNSW-NB15 and CIC-IDS2017 exist and are
   worth using over hand-joining CSV flow labels by 5-tuple and timestamp — that join is
   the single largest work item in the original plan, and it has largely been done.
2. Slice into time windows.
3. Run each window through **the existing Zeek service**. Do not write a second
   preprocessor — train/serve skew is the risk being avoided. Either `POST /analyze`
   against a locally served file, or import `app/parser.py` directly for bulk work.
4. Label each window from ground truth.
5. Generate reports with a teacher model, given Zeek JSON + labels. Spot-check.
6. Emit JSONL: `{"instruction", "input", "output"}` where `input` is byte-identical in
   shape to what `AnalysisPromptBuilder` produces.
7. Hold out a test split **by capture, never by window** — windows from one capture are
   correlated, and splitting across them leaks.

### Phase 2 — Training

Unsloth + ROCm on Linux. Start `max_seq_length: 8192`, batch 1, grad accum 8, r=16,
alpha=32, dropout 0.05, lr 2e-4 cosine, 1–2 epochs, all linear layers targeted.
Checkpoint often and evaluate format compliance at each one.

### Phase 3 — Evaluation and deployment

Score the held-out split on all three metrics, against the Phase 0 baseline. Then merge
the adapter, convert to GGUF, `ollama create` with a Modelfile, and point
`Ollama:Model` at it. **Nothing else in the application changes** — which is the payoff
for having kept the prompt construction server-side.

---

## Sources

- [bitsandbytes multi-backend / ROCm status](https://github.com/bitsandbytes-foundation/bitsandbytes/discussions/1339)
- [ROCm compatibility matrix](https://rocm.docs.amd.com/en/docs-7.0.0/compatibility/compatibility-matrix.html)
- [Unsloth official AMD support](https://unsloth.ai/docs/basics/amd)
- [Qwen3-4B-Instruct-2507](https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507)
- [Labelled PCAP releases of UNSW-NB15 / CIC-IDS2017](https://www.kaggle.com/datasets/yasiralifarrukh/unsw-and-cicids2017-labelled-pcap-data)
- [nids-datasets (packet + flow level)](https://github.com/rdpahalavan/nids-datasets)
