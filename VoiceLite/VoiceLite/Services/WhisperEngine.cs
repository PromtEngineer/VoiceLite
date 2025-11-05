using System;
using System.Threading;
using System.Threading.Tasks;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Adapter that wraps PersistentWhisperService to implement ITranscriptionEngine.
    /// Provides consistent interface for engine factory and allows seamless switching
    /// between Whisper and Parakeet engines.
    /// </summary>
    public class WhisperEngine : ITranscriptionEngine
    {
        private readonly PersistentWhisperService _whisperService;
        private readonly Settings _settings;
        private readonly IModelResolverService _modelResolver;

        public string EngineName => "Whisper";
        public bool RequiresGPU => false;
        public bool IsProcessing => _whisperService.IsProcessing;

        // Check if whisper.exe exists
        public bool IsAvailable
        {
            get
            {
                try
                {
                    var whisperExePath = _modelResolver.ResolveWhisperExePath();
                    var isAvailable = !string.IsNullOrEmpty(whisperExePath) &&
                                      System.IO.File.Exists(whisperExePath);

                    if (!isAvailable)
                        ErrorLogger.LogWarning($"Whisper unavailable: whisper.exe not found at {whisperExePath}");

                    return isAvailable;
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogWarning($"Whisper availability check failed: {ex.Message}");
                    return false;
                }
            }
        }

        // Forward events from PersistentWhisperService
        public event EventHandler<string>? TranscriptionComplete
        {
            add => _whisperService.TranscriptionComplete += value;
            remove => _whisperService.TranscriptionComplete -= value;
        }

        public event EventHandler<Exception>? TranscriptionError
        {
            add => _whisperService.TranscriptionError += value;
            remove => _whisperService.TranscriptionError -= value;
        }

        public event EventHandler<int>? ProgressChanged
        {
            add => _whisperService.ProgressChanged += value;
            remove => _whisperService.ProgressChanged -= value;
        }

        public WhisperEngine(PersistentWhisperService whisperService, Settings settings)
        {
            _whisperService = whisperService ?? throw new ArgumentNullException(nameof(whisperService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Create model resolver
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _modelResolver = new ModelResolverService(baseDir, null);
        }

        /// <summary>
        /// Transcribes audio using Whisper model specified in settings.
        /// </summary>
        public async Task<string> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken)
        {
            if (!IsAvailable)
                throw new InvalidOperationException("Whisper engine is not available");

            // Resolve model path based on settings
            var modelPath = _modelResolver.ResolveModelPath(_settings.WhisperModel);

            if (string.IsNullOrEmpty(modelPath) || !System.IO.File.Exists(modelPath))
            {
                throw new System.IO.FileNotFoundException($"Whisper model not found: {_settings.WhisperModel}");
            }

            // Use existing PersistentWhisperService transcription logic
            return await _whisperService.TranscribeAsync(audioFilePath, modelPath);
        }

        /// <summary>
        /// Warms up Whisper by running a dummy transcription.
        /// (PersistentWhisperService already does this in constructor)
        /// </summary>
        public async Task WarmUpAsync()
        {
            // PersistentWhisperService handles warmup in its constructor
            // This is a no-op for compatibility with ITranscriptionEngine
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _whisperService?.Dispose();
        }
    }
}
