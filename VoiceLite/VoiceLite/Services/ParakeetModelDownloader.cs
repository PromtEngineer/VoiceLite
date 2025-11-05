using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace VoiceLite.Services
{
    /// <summary>
    /// Downloads and manages Parakeet TDT v3 model files.
    /// Handles download with progress reporting, extraction, and verification.
    ///
    /// Model: NVIDIA Parakeet TDT 0.6B v3 (int8 quantized, multilingual)
    /// Size: ~640MB compressed, ~640MB extracted
    /// Source: https://github.com/k2-fsa/sherpa-onnx/releases
    /// </summary>
    public class ParakeetModelDownloader
    {
        private const string MODEL_URL = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8.tar.bz2";
        private const string MODEL_DIR_NAME = "parakeet-tdt-0.6b-v3-int8";
        private const long EXPECTED_DOWNLOAD_SIZE = 670_000_000; // ~640MB

        private readonly string _baseDir;
        private readonly string _parakeetDir;
        private readonly string _modelDir;

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30) // Large download timeout
        };

        public event EventHandler<int>? DownloadProgressChanged;
        public event EventHandler<string>? StatusChanged;

        public ParakeetModelDownloader(string? baseDir = null)
        {
            _baseDir = baseDir ?? AppDomain.CurrentDomain.BaseDirectory;
            _parakeetDir = Path.Combine(_baseDir, "parakeet");
            _modelDir = Path.Combine(_parakeetDir, MODEL_DIR_NAME);

            // Ensure parakeet directory exists
            Directory.CreateDirectory(_parakeetDir);
        }

        /// <summary>
        /// Checks if Parakeet model is already downloaded and valid.
        /// </summary>
        public async Task<bool> IsModelDownloadedAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Check if all required files exist
                    var encoderPath = Path.Combine(_modelDir, "encoder.int8.onnx");
                    var decoderPath = Path.Combine(_modelDir, "decoder.int8.onnx");
                    var joinerPath = Path.Combine(_modelDir, "joiner.int8.onnx");
                    var tokensPath = Path.Combine(_modelDir, "tokens.txt");

                    if (!File.Exists(encoderPath) || !File.Exists(decoderPath) ||
                        !File.Exists(joinerPath) || !File.Exists(tokensPath))
                    {
                        return false;
                    }

                    // Verify file sizes (rough check)
                    var encoderSize = new FileInfo(encoderPath).Length;
                    var decoderSize = new FileInfo(decoderPath).Length;
                    var joinerSize = new FileInfo(joinerPath).Length;

                    // encoder should be ~600MB+, decoder ~10MB+, joiner ~5MB+
                    if (encoderSize < 500_000_000 || decoderSize < 5_000_000 || joinerSize < 3_000_000)
                    {
                        ErrorLogger.LogWarning("Parakeet model files found but sizes are incorrect");
                        return false;
                    }

                    ErrorLogger.LogMessage("Parakeet model verified successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogWarning($"Parakeet model verification failed: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Gets the expected download size in bytes.
        /// </summary>
        public long GetExpectedDownloadSize() => EXPECTED_DOWNLOAD_SIZE;

        /// <summary>
        /// Gets the model directory path.
        /// </summary>
        public string GetModelPath() => _modelDir;

        /// <summary>
        /// Downloads and extracts the Parakeet model.
        /// Reports progress via DownloadProgressChanged event (0-100).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task DownloadModelAsync(CancellationToken cancellationToken)
        {
            var tempArchivePath = Path.Combine(_parakeetDir, "parakeet-model-temp.tar.bz2");

            try
            {
                // Check if already downloaded
                if (await IsModelDownloadedAsync())
                {
                    RaiseStatusChanged("Model already downloaded");
                    RaiseProgressChanged(100);
                    return;
                }

                // Phase 1: Download (0-80%)
                RaiseStatusChanged("Downloading Parakeet model (~640MB)...");
                await DownloadFileAsync(MODEL_URL, tempArchivePath, cancellationToken);

                // Phase 2: Extract (80-95%)
                RaiseStatusChanged("Extracting model files...");
                RaiseProgressChanged(80);
                await ExtractArchiveAsync(tempArchivePath, _parakeetDir, cancellationToken);
                RaiseProgressChanged(95);

                // Phase 3: Verify (95-100%)
                RaiseStatusChanged("Verifying model files...");
                var isValid = await IsModelDownloadedAsync();

                if (!isValid)
                {
                    throw new InvalidOperationException("Model verification failed after download");
                }

                // Cleanup temp file
                if (File.Exists(tempArchivePath))
                {
                    File.Delete(tempArchivePath);
                }

                RaiseProgressChanged(100);
                RaiseStatusChanged("Parakeet model downloaded successfully!");
                ErrorLogger.LogMessage("Parakeet model download completed successfully");
            }
            catch (OperationCanceledException)
            {
                // Cleanup on cancellation
                CleanupTempFiles(tempArchivePath);
                RaiseStatusChanged("Download cancelled");
                ErrorLogger.LogMessage("Parakeet model download cancelled by user");
                throw;
            }
            catch (Exception ex)
            {
                CleanupTempFiles(tempArchivePath);
                RaiseStatusChanged($"Download failed: {ex.Message}");
                ErrorLogger.LogError("Parakeet model download failed", ex);
                throw;
            }
        }

        /// <summary>
        /// Downloads a file with progress reporting.
        /// </summary>
        private async Task DownloadFileAsync(string url, string destinationPath, CancellationToken cancellationToken)
        {
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? EXPECTED_DOWNLOAD_SIZE;
                var bytesRead = 0L;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                        bytesRead += read;

                        // Report progress (0-80% for download phase)
                        var percentage = (int)((bytesRead * 80) / totalBytes);
                        RaiseProgressChanged(Math.Min(percentage, 80));

                        // Update status every 50MB
                        if (bytesRead % 50_000_000 < 8192)
                        {
                            RaiseStatusChanged($"Downloaded {bytesRead / 1_000_000}MB / {totalBytes / 1_000_000}MB");
                        }
                    }
                }

                ErrorLogger.LogMessage($"Parakeet model download complete: {bytesRead / 1_000_000}MB");
            }
        }

        /// <summary>
        /// Extracts tar.bz2 archive using SharpCompress.
        /// </summary>
        private async Task ExtractArchiveAsync(string archivePath, string destinationDir, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                using (var archive = ArchiveFactory.Open(archivePath))
                {
                    var totalEntries = 0;
                    var processedEntries = 0;

                    // Count entries
                    foreach (var _ in archive.Entries)
                        totalEntries++;

                    // Extract
                    foreach (var entry in archive.Entries)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw new OperationCanceledException();

                        if (!entry.IsDirectory)
                        {
                            entry.WriteToDirectory(destinationDir, new ExtractionOptions
                            {
                                ExtractFullPath = true,
                                Overwrite = true
                            });
                        }

                        processedEntries++;

                        // Report progress (80-95% for extraction phase)
                        var percentage = 80 + (processedEntries * 15 / totalEntries);
                        RaiseProgressChanged(percentage);
                    }
                }

                ErrorLogger.LogMessage("Parakeet model extraction complete");
            }, cancellationToken);
        }

        /// <summary>
        /// Deletes the model directory (for uninstall or cleanup).
        /// </summary>
        public async Task DeleteModelAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(_modelDir))
                    {
                        Directory.Delete(_modelDir, recursive: true);
                        ErrorLogger.LogMessage("Parakeet model deleted");
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("Failed to delete Parakeet model", ex);
                    throw;
                }
            });
        }

        /// <summary>
        /// Gets disk space required for download (download + extraction).
        /// </summary>
        public long GetRequiredDiskSpace()
        {
            // Need space for: compressed file (640MB) + extracted files (640MB) = ~1.3GB
            return EXPECTED_DOWNLOAD_SIZE * 2;
        }

        /// <summary>
        /// Checks if there's enough disk space for download.
        /// </summary>
        public bool HasEnoughDiskSpace()
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(_parakeetDir) ?? "C:\\");
                return drive.AvailableFreeSpace >= GetRequiredDiskSpace();
            }
            catch
            {
                return true; // Assume enough space if check fails
            }
        }

        private void CleanupTempFiles(string tempArchivePath)
        {
            try
            {
                if (File.Exists(tempArchivePath))
                {
                    File.Delete(tempArchivePath);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning($"Failed to cleanup temp file: {ex.Message}");
            }
        }

        private void RaiseProgressChanged(int percentage)
        {
            DownloadProgressChanged?.Invoke(this, Math.Clamp(percentage, 0, 100));
        }

        private void RaiseStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
            ErrorLogger.LogMessage($"Parakeet download status: {status}");
        }
    }
}
