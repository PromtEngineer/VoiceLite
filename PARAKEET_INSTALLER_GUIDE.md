# Parakeet Integration - Installer Guide

## sherpa-onnx Binaries Bundle Instructions

### What to Bundle

**sherpa-onnx binaries** must be bundled in the installer (~50MB).
**Parakeet model** should NOT be bundled (too large - user downloads in-app).

### Download sherpa-onnx Binaries

**Source:** https://huggingface.co/csukuangfj/sherpa-onnx-libs/tree/main
**Alternative:** https://github.com/k2-fsa/sherpa-onnx/releases/latest

**File to download:** `sherpa-onnx-v1.12.x-win-x64-static.tar.bz2` (or latest version)

### Required Files (from sherpa-onnx archive)

Extract the tar.bz2 and copy these files to `VoiceLite/sherpa-onnx/`:

```
VoiceLite/sherpa-onnx/
├── sherpa-onnx-offline.exe          # Main CLI executable (~500KB)
├── onnxruntime.dll                  # ONNX Runtime library (~10MB)
├── sherpa-onnx-c-api.dll           # C API library (~5MB)
├── sherpa-onnx-core.dll            # Core library (~3MB)
└── (other required DLLs from bin/)  # Various dependencies (~30MB total)
```

**Total size:** ~50MB

### Installer Script Changes (VoiceLiteSetup.iss)

Add the following to the `[Files]` section:

```iss
; ============================================================
; Parakeet Engine Support - sherpa-onnx binaries (Pro feature)
; ============================================================

; sherpa-onnx executable
Source: "{#AppDir}\sherpa-onnx\sherpa-onnx-offline.exe"; DestDir: "{app}\sherpa-onnx"; Flags: ignoreversion

; sherpa-onnx DLLs (ONNX Runtime + dependencies)
Source: "{#AppDir}\sherpa-onnx\*.dll"; DestDir: "{app}\sherpa-onnx"; Flags: ignoreversion

; NOTE: Parakeet model files NOT bundled (640MB - too large for installer)
; Pro users will download the model in-app via Settings > AI Models tab
```

### Installation Flow

1. **Installer runs** → Copies sherpa-onnx/ directory to Program Files
2. **User launches VoiceLite** → GPU detection runs automatically
3. **Pro user with NVIDIA GPU** → Sees "Download Parakeet Model" button in Settings
4. **User clicks download** → ParakeetModelDownloader downloads 640MB model
5. **Model extracted** → Parakeet engine becomes available

### Disk Space Requirements

**Installer:**
- VoiceLite app: ~100MB (existing)
- sherpa-onnx binaries: ~50MB (new)
- **Total installer size: ~150MB** (up from ~100MB)

**After Model Download (Pro users only):**
- Parakeet model: ~640MB (downloaded to %LOCALAPPDATA%\VoiceLite\parakeet)
- **Total disk usage: ~790MB** (app + binaries + model)

### Directory Structure After Installation

```
C:\Program Files\VoiceLite\
├── VoiceLite.exe                   # Main application
├── whisper/                        # Whisper binaries (existing)
│   ├── whisper.exe
│   ├── ggml-base.bin
│   └── ...
├── sherpa-onnx/                    # NEW: sherpa-onnx binaries
│   ├── sherpa-onnx-offline.exe    # Parakeet inference
│   ├── onnxruntime.dll
│   └── ...

%LOCALAPPDATA%\VoiceLite\
├── settings.json                   # User settings
├── parakeet/                       # NEW: Downloaded by user (Pro only)
│   └── parakeet-tdt-0.6b-v3-int8/ # Parakeet model (640MB)
│       ├── encoder.int8.onnx
│       ├── decoder.int8.onnx
│       ├── joiner.int8.onnx
│       └── tokens.txt
```

### Testing Checklist

- [ ] sherpa-onnx binaries included in build output
- [ ] Installer size ~150MB (acceptable)
- [ ] sherpa-onnx/ directory copied to Program Files
- [ ] sherpa-onnx-offline.exe runs successfully post-install
- [ ] No dependency errors (all DLLs present)
- [ ] Uninstaller removes sherpa-onnx/ directory
- [ ] Parakeet model NOT in installer (user downloads)

### Troubleshooting

**Issue:** sherpa-onnx-offline.exe won't run
**Fix:** Ensure all DLLs are in same directory (onnxruntime.dll, sherpa-onnx-core.dll, etc.)

**Issue:** Installer too large (>200MB)
**Fix:** Verify Parakeet model NOT bundled (should be user-downloaded)

**Issue:** Missing DLL error
**Fix:** Check sherpa-onnx archive for additional dependencies, copy all DLLs from bin/

### Build Process

1. Download sherpa-onnx binaries
2. Extract to `VoiceLite/sherpa-onnx/`
3. Add to git (track binaries)
4. Update VoiceLiteSetup.iss (add sherpa-onnx files section)
5. Build installer with Inno Setup
6. Test installer on clean machine
7. Verify sherpa-onnx-offline.exe runs

### Distribution

**Installer name:** `VoiceLite-Setup-v1.3.0.exe` (~150MB)

**What's included:**
- ✅ VoiceLite app
- ✅ Whisper binaries + base model
- ✅ sherpa-onnx binaries (NEW)
- ❌ Parakeet model (user downloads in-app)

**Download locations:**
- GitHub Releases (primary)
- Google Drive (mirror)
- voicelite.app (website)

### Version Notes

**v1.3.0 Changes:**
- Added sherpa-onnx binaries (~50MB)
- Installer size: ~100MB → ~150MB
- No breaking changes for existing users
- Parakeet support (Pro + GPU users only)

---

**Status:** Documentation Complete
**Next Step:** Download sherpa-onnx binaries and add to repository
**Estimated Installer Size:** 150MB (acceptable for desktop app with AI capabilities)
