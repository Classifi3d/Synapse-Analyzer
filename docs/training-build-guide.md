# Build guide: fine-tuning the Synapse analysis model

Hands-on instructions for the AMD training machine (RX 7700 XT, 12 GB, gfx1101 · 32 GB RAM).

Companion to [training-plan.md](training-plan.md), which explains *why*. This document is
the *how*. Work through it in order — Phase 2 is a control measurement and later phases
are meaningless without it.

> Command versions move. Where a version number appears below, check it against the
> linked upstream docs before running.

---

## Phase 0 — Machine setup

### 0.1 Operating system

**Install Ubuntu 24.04 LTS.** ROCm on Windows is significantly more limited, and every
step below assumes Linux.

### 0.2 ROCm

Your GPU is `gfx1101`, which is officially supported — no `HSA_OVERRIDE_GFX_VERSION`
workaround is needed.

```bash
wget https://repo.radeon.com/amdgpu-install/7.2.4/ubuntu/noble/amdgpu-install_7.2.4.70204-1_all.deb
sudo apt install ./amdgpu-install_7.2.4.70204-1_all.deb
sudo apt update
```

```bash
sudo apt install "linux-headers-$(uname -r)" "linux-modules-extra-$(uname -r)"
sudo apt install amdgpu-dkms
```

```bash
sudo apt install python3-setuptools python3-wheel
sudo usermod -a -G render,video $LOGNAME
sudo apt install rocm
```

Reboot, then verify. **Do not continue until both of these show your card:**

```bash
rocminfo | grep -i gfx
rocm-smi
```

You are looking for `gfx1101`. If `rocminfo` is empty, the group membership has not taken
effect (log out fully) or the DKMS module failed to build (`dkms status`).

### 0.3 Unsloth

```bash
sudo apt install python3.12-venv -y
python3.12 -m venv ~/synapse-train
source ~/synapse-train/bin/activate
pip install uv
```

Check your ROCm version, then install the matching PyTorch build:

```bash
amd-smi version
```

```bash
uv pip install "torch>=2.4,<2.11.0" "torchvision<0.26.0" "torchaudio<2.11.0" \
    --index-url https://download.pytorch.org/whl/rocm7.1 --upgrade --force-reinstall
```

```bash
uv pip install unsloth[amd]
```

ROCm-compatible bitsandbytes is a **separate, required** install — the PyPI wheel is
CUDA-only and will fail at runtime:

```bash
pip install --force-reinstall --no-cache-dir --no-deps \
  "https://github.com/bitsandbytes-foundation/bitsandbytes/releases/download/continuous-release_main/bitsandbytes-1.33.7.preview-py3-none-manylinux_2_24_x86_64.whl"
```

### 0.4 Verify the GPU is actually usable

Do this now. Discovering it at the end of a data-prep week is expensive.

```bash
python -c "
import torch
print('torch:', torch.__version__)
print('rocm/hip:', torch.version.hip)
print('gpu available:', torch.cuda.is_available())
print('device:', torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'NONE')
x = torch.randn(4096, 4096, device='cuda')
print('matmul ok:', (x @ x).shape)
"
```

Then confirm 4-bit quantisation works, which is the part most likely to break on ROCm:

```bash
python -c "
from unsloth import FastLanguageModel
m, t = FastLanguageModel.from_pretrained(
    'unsloth/Qwen3-4B-Instruct-2507', max_seq_length=2048, load_in_4bit=True)
print('4-bit load OK')
"
```

### 0.5 Other tools

```bash
sudo apt install zeek tshark   # tshark provides editcap
curl -fsSL https://ollama.com/install.sh | sh
```

---

## Phase 1 — Understand the data contract

This is the part that decides whether the fine-tune helps or hurts. **The training input
must be byte-identical in shape to what the app builds at inference.**

The prompt is assembled by
`SyanpseApplication_API/Application/Services/AnalysisPromptBuilder.cs`. Its structure:

```
<system instructions: role, threat categories, grounding rules, 5 required sections>

Finish your response with a single final line in exactly this format:
VERDICT: <NONE|LOW|MEDIUM|HIGH|CRITICAL> - <one sentence justification>

## Capture
File name: ...
Zeek processing time: ...s
Sampled logs (only the first 40 rows shown): conn.log, dns.log

## Zeek Summary
{"Connections":1100,"DnsQueries":300,...}

## Zeek Logs

### conn.log (200 rows)
{"ts":...,"id.orig_h":"10.0.0.5",...}
...

## Analyst Request
...
```

Two traps that will silently poison a dataset if you reimplement this in Python:

1. **The summary is serialised in PascalCase**, not snake_case. `AnalysisPromptBuilder`
   uses a plain `JsonSerializerOptions` with no naming policy, so it emits
   `Connections`, `DnsQueries`, `TopTalkers`. The snake_case you see on the wire between
   the API and the Zeek service is a *different* serialiser. The log rows, by contrast,
   are Zeek's raw output (`id.orig_h`, `ts`).
2. **The row-count header does not match the rows printed.** The heading says
   `### conn.log (200 rows)` — the number of rows Zeek returned — but only
   `Analysis:PromptRowsPerLog` (40) are actually printed. Replicate this exactly. If you
   would rather fix it, fix the C# *and* regenerate the dataset, so the two never
   diverge.

### The safe way: generate prompts with the real builder

Rather than reimplement, call the actual C# code. Zero skew, by construction.

Create `tools/PromptGen/PromptGen.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../SyanpseApplication_API/Application/Application.csproj" />
  </ItemGroup>
</Project>
```

`tools/PromptGen/Program.cs`:

```csharp
using System.Text.Json;
using Application.DTOs;
using Application.Options;
using Application.Services;
using Microsoft.Extensions.Options;

// usage: PromptGen <zeek-result.json> <file-name> [analyst-request]
// Emits the exact prompt the API would build, on stdout.

var snake = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    PropertyNameCaseInsensitive = true
};

var zeek = JsonSerializer.Deserialize<ZeekAnalysisResultDto>(
    File.ReadAllText(args[0]), snake)!;

var builder = new AnalysisPromptBuilder(Options.Create(new AnalysisOptions()));

Console.Out.Write(builder.Build(
    args[1],
    args.Length > 2 ? args[2] : null,
    zeek));
```

```bash
dotnet build tools/PromptGen -c Release
```

---

## Phase 2 — Baseline (do not skip)

You cannot claim the fine-tune helped without a control.

```bash
ollama pull qwen3:4b-instruct
```

Point the API at it and run 30–50 captures through the real application:

```json
"Ollama": { "Model": "qwen3:4b-instruct", "ContextLength": 32768 }
```

Score every output with the evaluator from Phase 5 and **keep the numbers**. This is the
row your write-up compares against.

---

## Phase 3 — Build the dataset

### 3.1 Acquire labelled pcaps

Use pre-labelled PCAP releases of CIC-IDS2017 and UNSW-NB15 rather than joining CSV flow
labels to packets yourself — that join, by 5-tuple and timestamp, is the largest single
work item in the original plan and it has largely been done already. See the sources in
[training-plan.md](training-plan.md).

Add MACCDC captures for **evaluation only** — they are unlabelled.

### 3.2 Slice into time windows

The unit of training is a *window*, not a flow: one window → one Zeek JSON → one report.
`editcap` splits on wall-clock seconds.

```bash
mkdir -p windows
editcap -i 300 raw/Wednesday-workingHours.pcap windows/wed.pcap
```

This produces `windows/wed_00001_*.pcap`, one file per 5 minutes.

### 3.3 Run Zeek and build the JSONL

`build_dataset.py` — run each window through Zeek exactly as the service does, produce
the prompt with the real builder, and pair it with a teacher-written report.

```python
#!/usr/bin/env python3
"""Turn windowed pcaps into a training JSONL."""
import json, subprocess, sys, tempfile
from pathlib import Path

# Reuse the service's own parser so the JSON matches production exactly.
sys.path.insert(0, str(Path(__file__).parent.parent / "SyanpseApplication_ZeekService"))
from app.parser import parse_logs

PROMPTGEN = "tools/PromptGen/bin/Release/net10.0/PromptGen"
ANALYST_REQUEST = "Produce a general threat assessment of this capture."


def run_zeek(pcap: Path, workspace: Path) -> dict:
    """Identical invocation to zeek_runner.run_zeek."""
    subprocess.run(
        ["zeek", "-C", "-r", str(pcap), "local", "LogAscii::use_json=T"],
        cwd=workspace, check=True, capture_output=True,
    )
    summary, logs, truncated = parse_logs(workspace)
    return {
        "success": True,
        "error": None,
        "duration_seconds": 0.0,
        "summary": json.loads(summary.model_dump_json(by_alias=True)),
        "logs": json.loads(logs.model_dump_json()),
        "truncated_logs": truncated,
    }


def to_snake(result: dict) -> dict:
    """PromptGen deserializes with SnakeCaseLower; pydantic already emits snake_case."""
    return result


def build_prompt(zeek_json: dict, file_name: str) -> str:
    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as fh:
        json.dump(to_snake(zeek_json), fh)
        path = fh.name
    out = subprocess.run(
        [PROMPTGEN, path, file_name, ANALYST_REQUEST],
        check=True, capture_output=True, text=True,
    )
    return out.stdout


def main(windows_dir: str, labels_file: str, out_file: str) -> None:
    labels = json.loads(Path(labels_file).read_text())  # {window_name: [attack, ...]}

    with open(out_file, "w") as out:
        for pcap in sorted(Path(windows_dir).glob("*.pcap")):
            with tempfile.TemporaryDirectory() as workspace:
                try:
                    zeek_json = run_zeek(pcap, Path(workspace))
                except subprocess.CalledProcessError as exc:
                    print(f"skip {pcap.name}: zeek failed: {exc.stderr[:200]}")
                    continue

            # A window with no connections teaches nothing.
            if zeek_json["summary"]["connections"] == 0:
                continue

            prompt = build_prompt(zeek_json, pcap.name)
            ground_truth = labels.get(pcap.name, [])

            out.write(json.dumps({
                "capture": pcap.name.rsplit("_", 2)[0],   # for grouped splitting
                "prompt": prompt,
                "labels": ground_truth,
                "response": None,                        # filled in by Phase 3.4
            }) + "\n")
            print(f"ok {pcap.name}: {zeek_json['summary']['connections']} conns")


if __name__ == "__main__":
    main(*sys.argv[1:4])
```

```bash
python build_dataset.py windows labels.json dataset.raw.jsonl
```

### 3.4 Generate the reports (teacher distillation)

Hand-writing thousands of reports is not feasible. Use a large model, giving it **both**
the Zeek JSON and the ground-truth labels, so it explains a known answer rather than
guessing one.

```python
#!/usr/bin/env python3
"""Fill in the `response` field with teacher-generated reports."""
import json, sys, requests

TEACHER = "qwen3:30b"      # or any large model you can run / call
OLLAMA = "http://localhost:11434/api/generate"

GUIDANCE = """
The ground truth for this capture window is: {labels}

Write the analyst report the Synapse prompt above asks for. It must:
  - reach a verdict consistent with the ground truth
  - justify that verdict ONLY from rows visible in the Zeek data
  - never mention the ground truth, or that you were given it
  - never cite a host, domain or port that does not appear in the data
  - end with the exact VERDICT line the prompt specifies
"""

def main(in_file: str, out_file: str) -> None:
    with open(in_file) as src, open(out_file, "w") as dst:
        for line in src:
            row = json.loads(line)
            labels = row["labels"] or ["benign traffic only"]

            response = requests.post(OLLAMA, json={
                "model": TEACHER,
                "prompt": row["prompt"] + GUIDANCE.format(labels=", ".join(labels)),
                "stream": False,
                "options": {"num_ctx": 32768, "temperature": 0.3},
            }, timeout=900).json()["response"]

            # Cheap guards. Anything failing these is not training data.
            if "VERDICT:" not in response:
                print(f"drop {row['capture']}: no verdict line")
                continue

            row["response"] = response
            dst.write(json.dumps(row) + "\n")

if __name__ == "__main__":
    main(*sys.argv[1:3])
```

**Read a random 30 of these yourself.** Teacher output is where silent quality problems
enter a dataset, and no downstream metric will catch a plausible-sounding fabrication.

### 3.5 Split — by capture, never by window

Windows from the same capture share hosts and timing. Splitting across them leaks the
test set into training and inflates every score.

```python
import json, random
from collections import defaultdict

rows = [json.loads(l) for l in open("dataset.jsonl")]
by_capture = defaultdict(list)
for r in rows:
    by_capture[r["capture"]].append(r)

captures = sorted(by_capture)
random.Random(42).shuffle(captures)
split = int(len(captures) * 0.85)

for name, group in (("train", captures[:split]), ("test", captures[split:])):
    with open(f"{name}.jsonl", "w") as fh:
        for capture in group:
            for row in by_capture[capture]:
                fh.write(json.dumps(row) + "\n")
```

Also check the balance before training. You want **benign well represented** — the prompt
instructs the model to say so plainly when traffic is clean, and starving it of benign
examples trains it to cry wolf:

```bash
python -c "
import json, collections
c = collections.Counter(
    'benign' if not json.loads(l)['labels'] else 'attack' for l in open('train.jsonl'))
print(c)
"
```

---

## Phase 4 — Train

`train.py`:

```python
from unsloth import FastLanguageModel
from unsloth.chat_templates import train_on_responses_only
from datasets import load_dataset
from trl import SFTTrainer, SFTConfig

MAX_SEQ = 8192          # raise only if VRAM allows; this is the binding constraint

model, tokenizer = FastLanguageModel.from_pretrained(
    model_name    = "unsloth/Qwen3-4B-Instruct-2507",
    max_seq_length= MAX_SEQ,
    load_in_4bit  = True,
)

model = FastLanguageModel.get_peft_model(
    model,
    r = 16,
    lora_alpha = 32,
    lora_dropout = 0.05,
    bias = "none",
    # Every linear layer, not just q/v. Bigger quality win than tuning rank,
    # for the same memory.
    target_modules = ["q_proj", "k_proj", "v_proj", "o_proj",
                      "gate_proj", "up_proj", "down_proj"],
    use_gradient_checkpointing = "unsloth",   # mandatory at this sequence length
    random_state = 3407,
)

def to_chat(row):
    return {"text": tokenizer.apply_chat_template(
        [{"role": "user",      "content": row["prompt"]},
         {"role": "assistant", "content": row["response"]}],
        tokenize=False)}

train = load_dataset("json", data_files="train.jsonl", split="train").map(to_chat)

trainer = SFTTrainer(
    model = model,
    tokenizer = tokenizer,
    train_dataset = train,
    args = SFTConfig(
        max_seq_length = MAX_SEQ,
        # Batch MUST be 1. At ~9k tokens per example, 4 would be ~37k tokens
        # in one backward pass - far past 12 GB.
        per_device_train_batch_size = 1,
        gradient_accumulation_steps = 8,      # effective batch 8
        warmup_ratio = 0.03,
        num_train_epochs = 2,                 # more invites forgetting
        learning_rate = 2e-4,
        lr_scheduler_type = "cosine",
        logging_steps = 5,
        save_steps = 100,
        optim = "adamw_8bit",
        bf16 = True,
        output_dir = "outputs",
        report_to = "none",
    ),
)

# Critical. Without this, loss is computed over the ~9k-token prompt as well as the
# ~500-token report, so >90% of the training signal is "predict the next Zeek log
# row" - wasted capacity, and it actively teaches the model to emit telemetry.
trainer = train_on_responses_only(
    trainer,
    instruction_part = "<|im_start|>user\n",
    response_part    = "<|im_start|>assistant\n",
)

trainer.train()
model.save_pretrained("lora_adapter")
tokenizer.save_pretrained("lora_adapter")
```

```bash
python train.py
```

Watch `rocm-smi` in another terminal. If you hit out-of-memory:

1. drop `MAX_SEQ` to 6144, then 4096
2. lower `Analysis:PromptRowsPerLog` in the API and **regenerate the dataset** — a
   shorter prompt at inference is better than a truncated one
3. only then consider a smaller base model

---

## Phase 5 — Evaluate

Three metrics. Verdict accuracy alone will not tell you whether the model became
unusable.

`evaluate.py`:

```python
#!/usr/bin/env python3
import json, re, sys, requests

SECTIONS = ["Executive Summary", "Findings", "Indicators of Compromise",
            "Risk Assessment", "Recommendations"]
VERDICT = re.compile(r"VERDICT:\s*(NONE|LOW|MEDIUM|HIGH|CRITICAL)", re.I)
IPV4 = re.compile(r"\b(?:\d{1,3}\.){3}\d{1,3}\b")

def evaluate(model: str, test_file: str) -> None:
    tp = fp = tn = fn = 0
    formatted = hallucinated = total = 0

    for line in open(test_file):
        row = json.loads(line)
        out = requests.post("http://localhost:11434/api/generate", json={
            "model": model, "prompt": row["prompt"], "stream": False,
            "options": {"num_ctx": 32768, "temperature": 0.2},
        }, timeout=900).json()["response"]

        total += 1

        # 1. format compliance
        match = VERDICT.search(out)
        if match and all(s.lower() in out.lower() for s in SECTIONS):
            formatted += 1

        # 2. verdict F1 (NONE -> benign, anything else -> malicious)
        predicted_threat = bool(match) and match.group(1).upper() != "NONE"
        actual_threat = bool(row["labels"])
        if   predicted_threat and actual_threat:     tp += 1
        elif predicted_threat and not actual_threat: fp += 1
        elif not predicted_threat and actual_threat: fn += 1
        else:                                        tn += 1

        # 3. hallucination: any IP cited that is absent from the input
        cited = set(IPV4.findall(out))
        present = set(IPV4.findall(row["prompt"]))
        if cited - present:
            hallucinated += 1

    precision = tp / (tp + fp) if tp + fp else 0
    recall    = tp / (tp + fn) if tp + fn else 0
    f1 = 2 * precision * recall / (precision + recall) if precision + recall else 0

    print(f"model              : {model}")
    print(f"verdict F1         : {f1:.3f}  (P {precision:.3f} / R {recall:.3f})")
    print(f"format compliance  : {formatted}/{total} ({formatted / total:.1%})")
    print(f"hallucinated hosts : {hallucinated}/{total} ({hallucinated / total:.1%})")

if __name__ == "__main__":
    evaluate(sys.argv[1], sys.argv[2])
```

Run it against **both** the Phase 2 baseline and the tuned model:

```bash
python evaluate.py qwen3:4b-instruct test.jsonl     # control
python evaluate.py synapse-analyst    test.jsonl     # tuned
```

If format compliance *fell*, the model has forgotten more than it learned — reduce
epochs or learning rate and retrain. That is the failure mode to watch for.

---

## Phase 6 — Deploy

```python
# merge.py
from unsloth import FastLanguageModel

model, tokenizer = FastLanguageModel.from_pretrained("lora_adapter", load_in_4bit=False)
model.save_pretrained_gguf("synapse-gguf", tokenizer, quantization_method="q4_k_m")
```

`Modelfile`:

```
FROM ./synapse-gguf/unsloth.Q4_K_M.gguf

PARAMETER num_ctx 32768
PARAMETER temperature 0.2
PARAMETER stop "<|im_end|>"
```

```bash
ollama create synapse-analyst -f Modelfile
ollama run synapse-analyst "test"
```

Then, on the machine running the API, change **one setting**:

```json
"Ollama": { "Model": "synapse-analyst", "ContextLength": 32768 }
```

Nothing else in the application changes. That is the payoff for keeping prompt
construction server-side in `AnalysisPromptBuilder` rather than baking it into the model.

---

## Checklist

- [ ] `rocminfo` shows `gfx1101`
- [ ] `torch.cuda.is_available()` is True
- [ ] 4-bit model load succeeds
- [ ] **Baseline scored and recorded**
- [ ] Windows sliced; benign well represented
- [ ] Prompts generated by the real C# builder
- [ ] 30 teacher reports read by a human
- [ ] Split by capture, not window
- [ ] `train_on_responses_only` applied
- [ ] Tuned model beats baseline on all three metrics
- [ ] GGUF loads in Ollama and the app runs unchanged

---

## Sources

- [ROCm install quick start](https://rocm.docs.amd.com/projects/install-on-linux/en/latest/install/quick-start.html)
- [ROCm compatibility matrix](https://rocm.docs.amd.com/en/docs-7.0.0/compatibility/compatibility-matrix.html)
- [Unsloth AMD install guide](https://unsloth.ai/docs/get-started/install/amd)
- [Unsloth Qwen3-2507 guide](https://unsloth.ai/docs/models/tutorials/qwen3-how-to-run-and-fine-tune/qwen3-2507)
- [Qwen3-4B-Instruct-2507](https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507)
