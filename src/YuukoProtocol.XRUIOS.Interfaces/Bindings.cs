using Standart.Hash.xxHash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YuukoProtocol
{
    public class Bindings
    {
        public sealed class DirectoryManager
        {
            private readonly string _bindingsFolder;
            private readonly Dictionary<string, DirectoryBinding> _bindings = new();

            internal string ThisDeviceId => Environment.MachineName;

            public void UpdateBindingInMemory(string uuid, Bindings.DirectoryBinding newBinding)
            {
                if (_bindings.ContainsKey(uuid))
                {
                    _bindings[uuid] = newBinding;
                }
            }

            public DirectoryManager(string bindingsFolder)
            {
                _bindingsFolder = Path.GetFullPath(bindingsFolder);
                Directory.CreateDirectory(_bindingsFolder);
            }

            public async Task LoadBindings(CancellationToken ct = default)
            {
                _bindings.Clear();
                if (!Directory.Exists(_bindingsFolder)) return;

                foreach (var file in Directory.EnumerateFiles(_bindingsFolder, "*.binding"))
                {
                    try
                    {
                        var bytes = await File.ReadAllBytesAsync(file, ct);
                        var binding = await BinaryConverter.NCByteArrayToObjectAsync<DirectoryBinding>(bytes, cancellationToken: ct);
                        if (binding != null)
                            _bindings[binding.Ref.DirectoryId] = binding;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load binding {Path.GetFileName(file)}: {ex.Message}");

                    }
                }
            }

            public IEnumerable<DirectoryBinding> GetAllBindings() => _bindings.Values;

            public DirectoryBinding? GetBindingById(string uuid)
            {
                _bindings.TryGetValue(uuid, out var binding);
                Console.WriteLine($"Looking up {uuid} → found? {(binding != null ? "YES" : "NO")}");
                return binding;
            }

            public async Task<string?> GetDirectoryById(string directoryId, string? defaultFolder = null, CancellationToken ct = default)
            {
                if (!_bindings.TryGetValue(directoryId, out var binding)) return null;

                // Prefer the newer resolution if present
                if (binding.Resolution != null &&
                    Directory.Exists(binding.Resolution.ResolvedPath))
                {
                    return binding.Resolution.ResolvedPath;
                }

                // Fallback to original reference (You guys have NO clue how important this was but how overlooked it was too)
                if (binding.Ref.OriginalDevice == ThisDeviceId &&
                    Directory.Exists(binding.Ref.FullPath))
                {
                    return binding.Ref.FullPath;
                }


                if (defaultFolder == null) return null;

                var resolvedPath = ResolveDirectory(binding, defaultFolder);
                await SaveBinding(binding, ct);

                return resolvedPath;
            }

            public async Task<(string Uuid, string ResolvedPath)> GetOrCreateDirectory(string fullPath, string? localOverride = null, CancellationToken ct = default)
            {
                var directoryRef = new DirectoryRef(fullPath, ThisDeviceId);

                if (!_bindings.TryGetValue(directoryRef.DirectoryId, out var binding))
                {
                    binding = new DirectoryBinding(directoryRef);
                    _bindings[directoryRef.DirectoryId] = binding;
                }

                var resolvedPath = ResolveDirectory(binding, localOverride);
                await SaveBinding(binding, ct);

                return (directoryRef.DirectoryId, resolvedPath);
            }

            public string ResolveDirectory(DirectoryBinding binding, string? defaultFolder = null)
            {
                if (binding.Resolution != null &&
                    Directory.Exists(binding.Resolution.ResolvedPath))
                {
                    return binding.Resolution.ResolvedPath;
                }

                if (Directory.Exists(binding.Ref.FullPath))
                {
                    if (binding.Resolution == null || !Directory.Exists(binding.Resolution.ResolvedPath))
                        binding.SetResolution(new DirectoryResolution(binding.Ref.FullPath, verified: true));

                    return binding.Ref.FullPath;
                }

                var pathToUse = defaultFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "XRUIOS", binding.Ref.DirectoryId);
                Directory.CreateDirectory(pathToUse);
                binding.SetResolution(new DirectoryResolution(pathToUse, verified: true));

                return pathToUse;
            }

            public async Task<bool> DeleteBinding(string directoryId, CancellationToken ct = default)
            {
                var bindingPath = Path.Combine(_bindingsFolder, directoryId + ".binding");
                if (!File.Exists(bindingPath)) return false;

                File.Delete(bindingPath);
                _bindings.Remove(directoryId);
                return true;
            }

            public async Task<bool> UpdateBinding(string directoryId, DirectoryResolution newResolution, CancellationToken ct = default)
            {
                if (!_bindings.TryGetValue(directoryId, out var binding)) return false;

                binding.SetResolution(newResolution);
                await SaveBinding(binding, ct);
                return true;

            }

            private async Task SaveBinding(DirectoryBinding binding, CancellationToken ct)
            {
                string path = Path.Combine(_bindingsFolder, binding.Ref.DirectoryId + ".binding");
                var data = await BinaryConverter.NCObjectToByteArrayAsync(binding, cancellationToken: ct);
                await File.WriteAllBytesAsync(path, data, ct);
            }
        }

        public sealed class DirectoryBinding
        {
            public DirectoryRef Ref { get; set; }  // private setter
            public DirectoryResolution? Resolution { get; set; }

            public DirectoryBinding() { }

            public DirectoryBinding(DirectoryRef directoryRef)
            {
                Ref = directoryRef;
            }

            public void SetResolution(DirectoryResolution resolution) => Resolution = resolution;
            public void ClearResolution() => Resolution = null;

            public void SetRef(DirectoryRef newRef) => Ref = newRef;
        }


        public readonly struct DirectoryRef
        {
            public string FullPath { get; init; } = string.Empty;  // ← init for safety
            public string OriginalDevice { get; init; } = string.Empty;
            public string DirectoryId { get; init; } = string.Empty;

            private const ulong HashSeed = 0x59554B4F;

            public DirectoryRef() { }

            public DirectoryRef(string fullPath, string originalDevice)
            {
                FullPath = fullPath ?? throw new ArgumentNullException(nameof(fullPath));
                OriginalDevice = originalDevice ?? throw new ArgumentNullException(nameof(originalDevice));
                DirectoryId = ComputeDirectoryId(fullPath, originalDevice).GetAwaiter().GetResult();
            }

            private static async Task<string> ComputeDirectoryId(string path, string device)
            {
                string canonical = $"{device}|{Path.GetFullPath(path)}";
                await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(canonical));
                ulong hash = await xxHash64.ComputeHashAsync(stream, bufferSize: 81920, seed: HashSeed);
                return hash.ToString("X16");
            }
        }
        public record DirectoryResolution
        {
            public string ResolvedPath { get; set; }
            public DateTime LastVerifiedUtc { get; set; }
            public bool Verified { get; set; }

            public DirectoryResolution() { }

            public DirectoryResolution(string resolvedPath, bool verified = false)
            {
                ResolvedPath = resolvedPath ?? throw new ArgumentNullException(nameof(resolvedPath));
                Verified = verified;
                LastVerifiedUtc = DateTime.UtcNow;
            }
        }
    }

}
