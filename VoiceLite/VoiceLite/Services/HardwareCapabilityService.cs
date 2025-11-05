using System;
using System.Linq;
using System.Management;

namespace VoiceLite.Services
{
    /// <summary>
    /// Detects hardware capabilities for optimizing transcription engine selection.
    /// Primary use: GPU detection for Parakeet TDT (NVIDIA GPU-accelerated transcription).
    /// </summary>
    public static class HardwareCapabilityService
    {
        private static bool? _hasNvidiaGPU = null;
        private static string? _gpuName = null;

        /// <summary>
        /// Checks if system has NVIDIA GPU for hardware-accelerated transcription.
        /// Cached after first call for performance.
        /// </summary>
        /// <returns>True if NVIDIA GPU detected, false otherwise</returns>
        public static bool HasNvidiaGPU()
        {
            // Return cached result if available
            if (_hasNvidiaGPU.HasValue)
                return _hasNvidiaGPU.Value;

            try
            {
                // Query WMI for video controllers
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString();
                        if (!string.IsNullOrEmpty(name) &&
                            (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("RTX", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("GTX", StringComparison.OrdinalIgnoreCase) ||
                             name.Contains("Quadro", StringComparison.OrdinalIgnoreCase)))
                        {
                            _gpuName = name;
                            _hasNvidiaGPU = true;
                            ErrorLogger.LogMessage($"NVIDIA GPU detected: {name}");
                            return true;
                        }
                    }
                }

                _hasNvidiaGPU = false;
                ErrorLogger.LogMessage("No NVIDIA GPU detected - Parakeet will not be available");
                return false;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"GPU detection failed: {ex.Message}");
                _hasNvidiaGPU = false;
                return false;
            }
        }

        /// <summary>
        /// Gets the name of the detected NVIDIA GPU.
        /// </summary>
        /// <returns>GPU name (e.g., "NVIDIA GeForce RTX 3060"), or empty string if none detected</returns>
        public static string GetGPUName()
        {
            if (!_hasNvidiaGPU.HasValue)
                HasNvidiaGPU(); // Trigger detection

            return _gpuName ?? string.Empty;
        }

        /// <summary>
        /// Gets all available GPUs on the system (for diagnostic purposes).
        /// </summary>
        /// <returns>Array of GPU names</returns>
        public static string[] GetAllGPUs()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    return searcher.Get()
                        .Cast<ManagementObject>()
                        .Select(obj => obj["Name"]?.ToString() ?? "Unknown GPU")
                        .ToArray();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to enumerate GPUs: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Checks if CUDA runtime is available (optional - for future use).
        /// Currently not used, but reserved for more granular GPU capability detection.
        /// </summary>
        /// <returns>True if CUDA is available, false otherwise</returns>
        public static bool HasCUDASupport()
        {
            // FUTURE: Check for CUDA runtime DLLs in system path
            // For now, assume NVIDIA GPU = CUDA support
            // Parakeet uses ONNX Runtime which has its own CUDA detection
            return HasNvidiaGPU();
        }

        /// <summary>
        /// Gets estimated VRAM of NVIDIA GPU (for diagnostic purposes).
        /// Returns 0 if detection fails or no NVIDIA GPU.
        /// </summary>
        /// <returns>VRAM in MB</returns>
        public static long GetGPUMemoryMB()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString() ?? "";
                        if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                        {
                            var adapterRAM = obj["AdapterRAM"];
                            if (adapterRAM != null && ulong.TryParse(adapterRAM.ToString(), out var bytes))
                            {
                                var mb = bytes / 1024 / 1024;
                                ErrorLogger.LogMessage($"GPU VRAM detected: {mb}MB");
                                return (long)mb;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to get GPU memory: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Resets GPU detection cache (for testing purposes).
        /// </summary>
        public static void ResetCache()
        {
            _hasNvidiaGPU = null;
            _gpuName = null;
        }

        /// <summary>
        /// Gets a diagnostic summary of hardware capabilities.
        /// Useful for support/debugging.
        /// </summary>
        /// <returns>Multi-line string with hardware info</returns>
        public static string GetDiagnosticInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== Hardware Capabilities ===");

            var hasGPU = HasNvidiaGPU();
            info.AppendLine($"NVIDIA GPU: {(hasGPU ? "Yes" : "No")}");

            if (hasGPU)
            {
                info.AppendLine($"GPU Name: {GetGPUName()}");
                var vram = GetGPUMemoryMB();
                if (vram > 0)
                    info.AppendLine($"GPU Memory: {vram}MB");
            }

            info.AppendLine($"All GPUs: {string.Join(", ", GetAllGPUs())}");
            info.AppendLine($"CUDA Support: {(HasCUDASupport() ? "Yes" : "No")}");
            info.AppendLine($"Parakeet Available: {(hasGPU ? "Yes" : "No (requires NVIDIA GPU)")}");

            return info.ToString();
        }
    }
}
