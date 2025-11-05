using System;
using VoiceLite.Core.Interfaces.Features;
using VoiceLite.Core.Interfaces.Services;
using VoiceLite.Models;

namespace VoiceLite.Services
{
    /// <summary>
    /// Factory for creating transcription engines based on user settings and hardware capabilities.
    /// Handles automatic engine selection and graceful fallback from Parakeet to Whisper.
    ///
    /// Selection Priority:
    /// 1. User preference (PreferredEngine setting)
    /// 2. Hardware availability (GPU for Parakeet)
    /// 3. License status (Pro required for Parakeet)
    /// 4. Fallback to Whisper (always available)
    /// </summary>
    public static class TranscriptionEngineFactory
    {
        /// <summary>
        /// Creates the appropriate transcription engine based on settings and capabilities.
        /// </summary>
        /// <param name="settings">User settings</param>
        /// <param name="proFeatureService">Pro feature service for license validation</param>
        /// <param name="modelResolver">Optional model resolver (for Whisper)</param>
        /// <returns>ITranscriptionEngine instance (Whisper or Parakeet)</returns>
        public static ITranscriptionEngine Create(
            Settings settings,
            IProFeatureService proFeatureService,
            IModelResolverService? modelResolver = null)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (proFeatureService == null)
                throw new ArgumentNullException(nameof(proFeatureService));

            ErrorLogger.LogMessage("TranscriptionEngineFactory: Selecting engine...");

            // Auto-select mode: Choose best engine based on hardware and license
            if (settings.AutoSelectEngine)
            {
                return CreateAutoSelected(settings, proFeatureService, modelResolver);
            }

            // User preference mode: Honor user's explicit choice with fallback
            return CreateUserPreferred(settings, proFeatureService, modelResolver);
        }

        /// <summary>
        /// Auto-selects the best engine based on hardware and license.
        /// Priority: Parakeet (if Pro + GPU available) > Whisper (fallback)
        /// </summary>
        private static ITranscriptionEngine CreateAutoSelected(
            Settings settings,
            IProFeatureService proFeatureService,
            IModelResolverService? modelResolver)
        {
            // Try Parakeet first if conditions are met
            if (proFeatureService.CanUseParakeet && HardwareCapabilityService.HasNvidiaGPU())
            {
                var parakeet = new ParakeetEngine(settings);

                if (parakeet.IsAvailable)
                {
                    ErrorLogger.LogMessage($"Auto-selected Parakeet TDT v3 (GPU: {HardwareCapabilityService.GetGPUName()})");
                    return parakeet;
                }

                ErrorLogger.LogWarning("Parakeet preferred but unavailable - falling back to Whisper");
            }

            // Fallback to Whisper
            ErrorLogger.LogMessage("Auto-selected Whisper (CPU-optimized)");
            return CreateWhisperEngine(settings, proFeatureService, modelResolver);
        }

        /// <summary>
        /// Creates the user's preferred engine with graceful fallback.
        /// </summary>
        private static ITranscriptionEngine CreateUserPreferred(
            Settings settings,
            IProFeatureService proFeatureService,
            IModelResolverService? modelResolver)
        {
            switch (settings.PreferredEngine)
            {
                case TranscriptionEngine.Parakeet:
                    return TryCreateParakeet(settings, proFeatureService, modelResolver);

                case TranscriptionEngine.Whisper:
                default:
                    ErrorLogger.LogMessage("User preferred: Whisper");
                    return CreateWhisperEngine(settings, proFeatureService, modelResolver);
            }
        }

        /// <summary>
        /// Attempts to create Parakeet engine with validation and fallback.
        /// </summary>
        private static ITranscriptionEngine TryCreateParakeet(
            Settings settings,
            IProFeatureService proFeatureService,
            IModelResolverService? modelResolver)
        {
            // Validate Pro license
            if (!proFeatureService.CanUseParakeet)
            {
                ErrorLogger.LogWarning("Parakeet requires Pro license - falling back to Whisper");

                if (settings.EnableEngineFallback)
                    return CreateWhisperEngine(settings, proFeatureService, modelResolver);

                throw new InvalidOperationException(
                    "Parakeet requires VoiceLite Pro ($20 one-time payment). " +
                    "Upgrade at voicelite.app or switch to Whisper in Settings.");
            }

            // Validate NVIDIA GPU
            if (!HardwareCapabilityService.HasNvidiaGPU())
            {
                ErrorLogger.LogWarning("Parakeet requires NVIDIA GPU - falling back to Whisper");

                if (settings.EnableEngineFallback)
                    return CreateWhisperEngine(settings, proFeatureService, modelResolver);

                throw new InvalidOperationException(
                    "Parakeet requires NVIDIA GPU for hardware acceleration. " +
                    "No NVIDIA GPU detected. Switch to Whisper in Settings.");
            }

            // Try to create Parakeet
            var parakeet = new ParakeetEngine(settings);

            if (parakeet.IsAvailable)
            {
                ErrorLogger.LogMessage($"User preferred: Parakeet TDT v3 (GPU: {HardwareCapabilityService.GetGPUName()})");
                return parakeet;
            }

            // Parakeet unavailable (model not downloaded?)
            ErrorLogger.LogWarning("Parakeet model not found - falling back to Whisper");

            if (settings.EnableEngineFallback)
                return CreateWhisperEngine(settings, proFeatureService, modelResolver);

            throw new InvalidOperationException(
                "Parakeet model files not found. " +
                "Download the Parakeet model in Settings > AI Models tab, " +
                "or switch to Whisper.");
        }

        /// <summary>
        /// Creates Whisper engine (always available fallback).
        /// </summary>
        private static ITranscriptionEngine CreateWhisperEngine(
            Settings settings,
            IProFeatureService proFeatureService,
            IModelResolverService? modelResolver)
        {
            // Create ModelResolverService if not provided
            var resolver = modelResolver ?? new ModelResolverService(
                AppDomain.CurrentDomain.BaseDirectory,
                proFeatureService);

            // Wrap PersistentWhisperService in WhisperEngine adapter
            var whisperService = new PersistentWhisperService(settings, resolver, proFeatureService);
            return new WhisperEngine(whisperService, settings);
        }

        /// <summary>
        /// Gets diagnostic information about engine selection.
        /// Useful for troubleshooting and support.
        /// </summary>
        public static string GetSelectionDiagnostics(Settings settings, IProFeatureService proFeatureService)
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== Transcription Engine Selection ===");

            info.AppendLine($"Auto-select: {settings.AutoSelectEngine}");
            info.AppendLine($"Preferred Engine: {settings.PreferredEngine}");
            info.AppendLine($"Fallback Enabled: {settings.EnableEngineFallback}");
            info.AppendLine();

            info.AppendLine("Parakeet Availability:");
            info.AppendLine($"  Pro License: {(proFeatureService.CanUseParakeet ? "Yes" : "No")}");
            info.AppendLine($"  NVIDIA GPU: {(HardwareCapabilityService.HasNvidiaGPU() ? "Yes" : "No")}");

            if (HardwareCapabilityService.HasNvidiaGPU())
            {
                info.AppendLine($"  GPU Name: {HardwareCapabilityService.GetGPUName()}");
                var vram = HardwareCapabilityService.GetGPUMemoryMB();
                if (vram > 0)
                    info.AppendLine($"  GPU Memory: {vram}MB");
            }

            var parakeet = new ParakeetEngine(settings);
            info.AppendLine($"  Model Available: {parakeet.IsAvailable}");
            parakeet.Dispose();

            info.AppendLine();
            info.AppendLine("Whisper Availability:");
            info.AppendLine($"  Always Available: Yes");

            return info.ToString();
        }
    }
}
