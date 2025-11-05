using System;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceLite.Core.Interfaces.Services
{
    /// <summary>
    /// Common interface for speech-to-text transcription engines.
    /// Abstracts Whisper and Parakeet implementations for interchangeable use.
    /// </summary>
    public interface ITranscriptionEngine : IDisposable
    {
        /// <summary>
        /// Human-readable engine name (e.g., "Whisper", "Parakeet TDT v3")
        /// </summary>
        string EngineName { get; }

        /// <summary>
        /// Whether this engine requires NVIDIA GPU for optimal performance
        /// </summary>
        bool RequiresGPU { get; }

        /// <summary>
        /// Whether this engine is available on the current system
        /// (checks for binaries, models, and hardware requirements)
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Whether the engine is currently processing audio
        /// </summary>
        bool IsProcessing { get; }

        /// <summary>
        /// Transcribes audio file to text asynchronously.
        /// </summary>
        /// <param name="audioFilePath">Absolute path to WAV file (16kHz, mono, 16-bit)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Transcribed text</returns>
        /// <exception cref="InvalidOperationException">If engine is not available</exception>
        /// <exception cref="TimeoutException">If transcription exceeds timeout</exception>
        Task<string> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken);

        /// <summary>
        /// Warms up the engine by running a dummy transcription.
        /// Reduces latency for the first real transcription.
        /// </summary>
        Task WarmUpAsync();

        /// <summary>
        /// Fired when transcription completes successfully
        /// </summary>
        event EventHandler<string>? TranscriptionComplete;

        /// <summary>
        /// Fired when transcription fails
        /// </summary>
        event EventHandler<Exception>? TranscriptionError;

        /// <summary>
        /// Fired when progress updates are available (0-100)
        /// </summary>
        event EventHandler<int>? ProgressChanged;
    }
}
