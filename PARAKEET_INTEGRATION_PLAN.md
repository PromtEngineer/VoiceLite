# Parakeet TDT v3 Integration Plan

## Overview

Add NVIDIA Parakeet TDT 0.6B v3 (multilingual) support to VoiceLite as an optional Pro feature for GPU users. This provides 2-3x faster transcription with better accuracy (6% WER vs 19% WER) while maintaining fallback to Whisper for compatibility.

## Architecture Design

### 1. Abstraction Layer - ITranscriptionEngine

Create a common interface to abstract Whisper and Parakeet engines:

```csharp
public interface ITranscriptionEngine : IDisposable
{
    string EngineName { get; }
    bool RequiresGPU { get; }
    bool IsAvailable { get; }

    Task<string> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken);
    Task WarmUpAsync();
}
```

### 2. Engine Implementations

**WhisperEngine.cs** - Wraps existing PersistentWhisperService
```csharp
public class WhisperEngine : ITranscriptionEngine
{
    private readonly PersistentWhisperService _service;
    public string EngineName => "Whisper";
    public bool RequiresGPU => false;
    public bool IsAvailable => File.Exists(whisperExePath);
}
```

**ParakeetEngine.cs** - New sherpa-onnx integration
```csharp
public class ParakeetEngine : ITranscriptionEngine
{
    public string EngineName => "Parakeet TDT v3";
    public bool RequiresGPU => true;
    public bool IsAvailable => HasNvidiaGPU() && File.Exists(sherpaOnnxExePath);
}
```

### 3. Hardware Detection - HardwareCapabilityService

Detect GPU capabilities at runtime:

```csharp
public static class HardwareCapabilityService
{
    public static bool HasNvidiaGPU()
    {
        // Check via WMI: Win32_VideoController
        // Look for "NVIDIA" in name
        // Optional: Check CUDA availability
    }

    public static string GetGPUName()
    {
        // Return GPU model (e.g., "NVIDIA GeForce RTX 3060")
    }

    public static bool HasCUDASupport()
    {
        // Check if CUDA runtime is available
    }
}
```

### 4. Engine Factory - TranscriptionEngineFactory

Select appropriate engine based on user settings and hardware:

```csharp
public static class TranscriptionEngineFactory
{
    public static ITranscriptionEngine Create(Settings settings, IProFeatureService proFeatureService)
    {
        // Priority 1: User prefers Parakeet + Has Pro + Has GPU
        if (settings.PreferParakeet && proFeatureService.IsProUser && HardwareCapabilityService.HasNvidiaGPU())
        {
            var parakeet = new ParakeetEngine(settings);
            if (parakeet.IsAvailable)
                return parakeet;
        }

        // Fallback: Whisper (always available)
        return new WhisperEngine(settings);
    }
}
```

## Sherpa-ONNX Integration

### Binaries and Models

**Download Locations:**
- Binaries: https://github.com/k2-fsa/sherpa-onnx/releases (latest: v1.12.15+)
- Model: https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2

**Directory Structure:**
```
VoiceLite/
├── sherpa-onnx/
│   ├── sherpa-onnx-offline.exe         # Main CLI (Windows x64)
│   ├── onnxruntime.dll                 # ONNX Runtime
│   ├── sherpa-onnx-c-api.dll           # C API
│   └── (other dependencies)
├── parakeet/
│   └── parakeet-tdt-0.6b-v3-int8/
│       ├── encoder.int8.onnx           # 622MB
│       ├── decoder.int8.onnx           # 12MB
│       ├── joiner.int8.onnx            # 6.1MB
│       └── tokens.txt                  # 92KB
```

### Command-Line Interface

**Basic Usage:**
```bash
sherpa-onnx-offline.exe \
  --encoder=parakeet/parakeet-tdt-0.6b-v3-int8/encoder.int8.onnx \
  --decoder=parakeet/parakeet-tdt-0.6b-v3-int8/decoder.int8.onnx \
  --joiner=parakeet/parakeet-tdt-0.6b-v3-int8/joiner.int8.onnx \
  --tokens=parakeet/parakeet-tdt-0.6b-v3-int8/tokens.txt \
  --model-type=nemo_transducer \
  --num-threads=4 \
  audio.wav
```

**Expected Output:**
```
Transcription result goes here
```

## Settings Model Changes

**Add to Settings.cs:**
```csharp
public enum TranscriptionEngine
{
    Whisper,    // Default (CPU, universal)
    Parakeet    // Pro + GPU only
}

public class Settings
{
    // Existing properties...

    // NEW: Engine selection
    public TranscriptionEngine PreferredEngine { get; set; } = TranscriptionEngine.Whisper;

    // NEW: Parakeet-specific settings
    public bool AutoSelectEngine { get; set; } = true;  // Auto-select based on hardware
}
```

## UI Changes

### Settings Window - AI Models Tab

**Current:**
```
Model Selection:
[ ] Tiny (42MB) - Free
[x] Base (78MB) - Free (Default)
[ ] Small (253MB) - Pro
[ ] Medium (823MB) - Pro
[ ] Large (3.1GB) - Pro
```

**New:**
```
Transcription Engine:
( ) Whisper (CPU, All PCs)
( ) Parakeet TDT v3 (GPU Required) ⚡ [Pro]

[i] Parakeet detected: NVIDIA GeForce RTX 3060
[i] Parakeet offers 2-3x faster transcription with better accuracy

Model Selection (Whisper):
[ ] Tiny (42MB) - Free
[x] Base (78MB) - Free (Default)
[ ] Small (253MB) - Pro
[ ] Medium (823MB) - Pro
[ ] Large (3.1GB) - Pro

Model Selection (Parakeet):
[x] Parakeet TDT v3 Multilingual (640MB) - Pro [Download]
    - 25 European languages
    - 6% WER (vs 19% Whisper)
    - 10-50x faster with GPU
```

### Pro Feature Gating

**ProFeatureService.cs additions:**
```csharp
public bool CanUseParakeet => IsProUser;

public Visibility ParakeetEngineVisibility =>
    IsProUser && HardwareCapabilityService.HasNvidiaGPU()
        ? Visibility.Visible
        : Visibility.Collapsed;

public string ParakeetUnavailableReason()
{
    if (!IsProUser)
        return "Parakeet requires VoiceLite Pro ($20 one-time)";

    if (!HardwareCapabilityService.HasNvidiaGPU())
        return "Parakeet requires NVIDIA GPU (not detected)";

    return "";
}
```

## Model Download System

**Extend existing ModelDownloader (if exists) or create new:**

```csharp
public class ParakeetModelDownloader
{
    private const string MODEL_URL = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2";

    public async Task DownloadAsync(IProgress<int> progress, CancellationToken ct)
    {
        // 1. Download .tar.bz2 (640MB)
        // 2. Extract to VoiceLite/parakeet/
        // 3. Verify files exist
        // 4. Update settings
    }
}
```

## Installer Changes (VoiceLiteSetup.iss)

**DO NOT bundle Parakeet in installer** - Too large (640MB)
- Bundle sherpa-onnx binaries (~50MB) only
- Parakeet models downloadable in-app (Pro users)

```iss
[Files]
; Existing Whisper files...

; NEW: Sherpa-ONNX binaries (Pro feature infrastructure)
Source: "{#AppDir}\sherpa-onnx\sherpa-onnx-offline.exe"; DestDir: "{app}\sherpa-onnx"; Flags: ignoreversion
Source: "{#AppDir}\sherpa-onnx\*.dll"; DestDir: "{app}\sherpa-onnx"; Flags: ignoreversion

; NOTE: Parakeet models NOT included - downloaded in-app by Pro users
```

## Performance Characteristics

### Expected Performance (Parakeet TDT v3 int8)

**With NVIDIA GPU (RTX 2060+):**
- Transcription latency: **<100ms** (vs <200ms Whisper)
- Processing time: **~0.5s** (vs ~1.5s Whisper base)
- Accuracy: **6% WER** (vs 19% WER Whisper)
- Languages: **25 European** (vs 99 languages Whisper)

**CPU Fallback (No GPU):**
- Processing time: **~2s** (similar to Whisper)
- Still uses Whisper for better compatibility

### Memory Footprint

- Idle RAM: **<150MB** (vs <100MB Whisper)
- Active RAM: **<500MB** with GPU (vs <300MB Whisper)
- Model size: **640MB** (vs 78MB Whisper base)

## Testing Strategy

### Unit Tests (VoiceLite.Tests)

```csharp
[Fact]
public void HardwareCapabilityService_DetectsNvidiaGPU()
{
    var hasGPU = HardwareCapabilityService.HasNvidiaGPU();
    // Assert based on test machine
}

[Fact]
public async Task ParakeetEngine_TranscribesAudio_Success()
{
    // Arrange: Mock sherpa-onnx process
    var engine = new ParakeetEngine(settings);

    // Act
    var result = await engine.TranscribeAsync("test.wav", CancellationToken.None);

    // Assert
    Assert.NotEmpty(result);
}

[Fact]
public void TranscriptionEngineFactory_FallsBackToWhisper_WhenNoGPU()
{
    // Mock: No NVIDIA GPU
    var engine = TranscriptionEngineFactory.Create(settings, proFeatureService);

    Assert.IsType<WhisperEngine>(engine);
}
```

### Integration Tests

1. **GPU System Test** (Manual - Requires NVIDIA GPU)
   - Verify Parakeet option appears in Settings
   - Download Parakeet model
   - Transcribe 10 audio samples
   - Verify accuracy and speed

2. **CPU System Test** (Automated)
   - Verify Parakeet hidden when no GPU
   - Verify graceful fallback to Whisper
   - Verify no crashes/errors

3. **Pro/Free Test**
   - Free user: Parakeet hidden
   - Pro user + GPU: Parakeet visible
   - Pro user + no GPU: Parakeet visible but disabled with message

## Migration Path

### For Existing Users

1. **v1.2.1 Upgrade** (Current → Parakeet Support)
   - Settings.json auto-migration adds `PreferredEngine: "Whisper"`
   - No behavior change (Whisper remains default)
   - Pro users see new "Parakeet" option if GPU detected

2. **Opt-In Experience**
   - New Pro users: Prompt to try Parakeet on first launch (if GPU detected)
   - Existing Pro users: Info banner "New: Try Parakeet for 2-3x faster transcription"

### Backwards Compatibility

- ✅ Settings.json compatible (new fields have defaults)
- ✅ Whisper remains default engine
- ✅ No breaking changes to API/interfaces
- ✅ Installer size increase: +50MB (sherpa-onnx binaries only)

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| GPU detection fails | Fallback to Whisper, add diagnostic logs |
| Parakeet crashes | Catch exceptions, fallback to Whisper |
| Model download fails | Retry logic, mirror URLs, resume support |
| Increased support burden | GPU detection in error logs, clear Pro requirements |
| Installer size bloat | Don't bundle models (640MB), only binaries (50MB) |

## Implementation Phases

### Phase 1: Core Infrastructure (Day 1) ✅ **TODAY**
- [x] Research sherpa-onnx integration
- [ ] Create ITranscriptionEngine interface
- [ ] Create HardwareCapabilityService
- [ ] Download sherpa-onnx binaries
- [ ] Proof of concept: CLI transcription

### Phase 2: Parakeet Service (Day 2)
- [ ] Create ParakeetEngine.cs
- [ ] Implement process management
- [ ] Add error handling and fallback logic
- [ ] Unit tests for ParakeetEngine

### Phase 3: Settings & UI (Day 3)
- [ ] Update Settings model
- [ ] Update ProFeatureService
- [ ] Add Parakeet option to Settings UI
- [ ] GPU detection badge

### Phase 4: Model Download (Day 4)
- [ ] Create ParakeetModelDownloader
- [ ] Add progress UI for download
- [ ] Add to installer (sherpa-onnx binaries)

### Phase 5: Testing & Polish (Day 5)
- [ ] Integration tests
- [ ] Manual testing on GPU system
- [ ] Update documentation
- [ ] Commit and push

## Success Metrics

- ✅ Parakeet option appears for Pro users with NVIDIA GPU
- ✅ Transcription 2-3x faster than Whisper on GPU
- ✅ Accuracy improves (lower WER)
- ✅ Graceful fallback to Whisper on errors
- ✅ No regressions in Whisper performance
- ✅ Installer builds successfully
- ✅ All tests pass

## References

- [sherpa-onnx GitHub](https://github.com/k2-fsa/sherpa-onnx)
- [sherpa-onnx Documentation](https://k2-fsa.github.io/sherpa/onnx/)
- [Parakeet TDT v3 Model](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3)
- [NVIDIA Parakeet Blog](https://developer.nvidia.com/blog/pushing-the-boundaries-of-speech-recognition-with-nemo-parakeet-asr-models/)
