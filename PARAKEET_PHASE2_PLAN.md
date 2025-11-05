# Parakeet Phase 2: Binaries & Model Downloader Implementation Plan

## Download URLs

### sherpa-onnx Binaries (Windows x64)
**Primary Source:** https://huggingface.co/csukuangfj/sherpa-onnx-libs/tree/main
**GitHub Releases:** https://github.com/k2-fsa/sherpa-onnx/releases/latest
**Recommended File:** `sherpa-onnx-v1.12.x-win-x64-static.tar.bz2`

### Parakeet Model (TDT v3 int8)
**URL:** https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2
**Size:** ~640MB
**Contents:** encoder.int8.onnx (622MB), decoder.int8.onnx (12MB), joiner.int8.onnx (6.1MB), tokens.txt (92KB)

## Directory Structure

```
VoiceLite/
├── sherpa-onnx/                        # Bundled in installer (~50MB)
│   ├── sherpa-onnx-offline.exe         # Main CLI executable
│   ├── onnxruntime.dll                 # ONNX Runtime
│   ├── sherpa-onnx-c-api.dll          # C API library
│   ├── sherpa-onnx-core.dll           # Core library
│   └── (other DLLs)                    # Dependencies
├── parakeet/                           # Downloaded by user (Pro only)
│   └── parakeet-tdt-0.6b-v3-int8/     # Model directory
│       ├── encoder.int8.onnx           # 622MB
│       ├── decoder.int8.onnx           # 12MB
│       ├── joiner.int8.onnx            # 6.1MB
│       └── tokens.txt                  # 92KB
```

## Implementation Steps

### Step 1: Download sherpa-onnx Binaries
- [ ] Download latest release from HuggingFace or GitHub
- [ ] Extract tar.bz2 archive
- [ ] Copy required files to `VoiceLite/sherpa-onnx/`
- [ ] Test sherpa-onnx-offline.exe execution
- [ ] Add to git (binaries will be tracked)

### Step 2: Create ParakeetModelDownloader Service
- [ ] Create `ParakeetModelDownloader.cs` in Services/
- [ ] Implement download with progress reporting
- [ ] Implement tar.bz2 extraction (use SharpCompress NuGet)
- [ ] Implement file verification (check sizes, existence)
- [ ] Add cancellation support
- [ ] Add resume support (optional)

### Step 3: Update Settings UI
- [ ] Add "Transcription Engine" section to AI Models tab
- [ ] Add radio buttons: Whisper / Parakeet
- [ ] Add GPU detection badge
- [ ] Add "Download Parakeet Model" button
- [ ] Add progress bar for download
- [ ] Add Pro license requirement message

### Step 4: Update Installer
- [ ] Add sherpa-onnx/ directory to VoiceLiteSetup.iss
- [ ] DON'T bundle Parakeet model (too large - user downloads)
- [ ] Test installer size (~100MB → ~150MB)

### Step 5: Integration Testing
- [ ] Test on system with NVIDIA GPU (if available)
- [ ] Test on system without GPU (fallback to Whisper)
- [ ] Test model download flow
- [ ] Test engine switching
- [ ] Test transcription with both engines

## Required NuGet Packages

```xml
<!-- Add to VoiceLite.csproj -->
<PackageReference Include="SharpCompress" Version="0.37.2" />  <!-- For .tar.bz2 extraction -->
```

## ParakeetModelDownloader Interface

```csharp
public class ParakeetModelDownloader
{
    public event EventHandler<int>? DownloadProgressChanged;  // 0-100
    public event EventHandler<string>? StatusChanged;

    public async Task<bool> IsModelDownloadedAsync();
    public async Task DownloadModelAsync(CancellationToken cancellationToken);
    public async Task<long> GetModelSizeAsync();  // Returns size in bytes
    public string GetModelPath();
}
```

## Settings UI Mock (AI Models Tab)

```
┌──────────────────────────────────────────────────────────┐
│ AI Models                                                │
├──────────────────────────────────────────────────────────┤
│                                                          │
│ Transcription Engine:                                    │
│   ( ) Whisper (CPU, Universal)                          │
│   (•) Parakeet TDT v3 (GPU Accelerated) ⚡ [Pro]        │
│                                                          │
│   [i] NVIDIA GeForce RTX 3060 detected                  │
│   [i] 2-3x faster with better accuracy (6% vs 19% WER) │
│                                                          │
│ ─────────────────────────────────────────────────────── │
│                                                          │
│ Whisper Models:                                          │
│   [ ] Tiny (42MB) - Free                                │
│   [x] Base (78MB) - Free ⭐ Default                     │
│   [ ] Small (253MB) - Pro                               │
│   [ ] Medium (823MB) - Pro                              │
│   [ ] Large (3.1GB) - Pro                               │
│                                                          │
│ ─────────────────────────────────────────────────────── │
│                                                          │
│ Parakeet Model:                                          │
│   [ ] TDT v3 Multilingual (640MB) [Download]            │
│                                                          │
│   Status: Not downloaded                                 │
│   [Download Model (640MB)]                              │
│                                                          │
│   Progress: [===================>      ] 75%            │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

## Error Handling

### Scenario 1: No NVIDIA GPU
- Parakeet option grayed out
- Message: "Parakeet requires NVIDIA GPU (not detected)"
- Auto-fallback to Whisper

### Scenario 2: No Pro License
- Parakeet option visible but locked
- Message: "Upgrade to Pro ($20) to unlock Parakeet"
- Link to voicelite.app

### Scenario 3: Model Not Downloaded
- Parakeet selectable but shows warning
- Message: "Download Parakeet model to continue"
- One-click download button

### Scenario 4: Download Failed
- Show error message
- Offer retry
- Auto-fallback to Whisper for current session

## Testing Checklist

- [ ] sherpa-onnx-offline.exe runs successfully
- [ ] Parakeet model downloads and extracts correctly
- [ ] GPU detection works (on NVIDIA system)
- [ ] Pro license gating works (Free vs Pro)
- [ ] Engine switching works (Whisper ↔ Parakeet)
- [ ] Transcription works with Parakeet
- [ ] Fallback to Whisper works on errors
- [ ] Installer bundles sherpa-onnx correctly
- [ ] Installer size reasonable (~150MB)

## Success Criteria

✅ sherpa-onnx binaries bundled and functional
✅ Model downloader works with progress reporting
✅ Settings UI shows Parakeet option (Pro + GPU)
✅ Engine switching functional
✅ Transcription works with both engines
✅ Installer builds successfully
✅ Documentation updated

---

**Phase 2 Status:** Planning Complete → Ready for Implementation
**Estimated Time:** 20-30 hours
**Next:** Download sherpa-onnx binaries and set up directory structure
