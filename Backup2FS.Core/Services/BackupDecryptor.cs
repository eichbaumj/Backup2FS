using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Claunia.PropertyList;

namespace Backup2FS.Core.Services
{
    /// <summary>
    /// Decrypts an encrypted iOS (iTunes-style) backup into a standard, unencrypted backup
    /// folder — a decrypted Manifest.db plus every file decrypted back into its
    /// "&lt;first-2-hex&gt;/&lt;fileID&gt;" hashed layout — so the existing normalizer can process it.
    /// Algorithm ported from jsharkey13/iphone_backup_decrypt (see <see cref="BackupKeybag"/>).
    /// </summary>
    public sealed class BackupDecryptor
    {
        private readonly string _backupDir;
        private NSDictionary? _manifestPlist;
        private BackupKeybag? _keybag;
        private bool _unlocked;

        public event Action<string>? LogMessage;
        public event Action<int>? ProgressReport;

        public BackupDecryptor(string backupDirectory)
        {
            _backupDir = backupDirectory;
        }

        /// <summary>
        /// Reads Manifest.plist and attempts to unlock the keybag with the supplied passphrase.
        /// Returns true if the password is correct, false otherwise. Fast (no file decryption).
        /// </summary>
        public bool Unlock(string password)
        {
            string manifestPlistPath = Path.Combine(_backupDir, "Manifest.plist");
            if (!File.Exists(manifestPlistPath))
                throw new FileNotFoundException("Manifest.plist not found in backup folder.", manifestPlistPath);

            _manifestPlist = (NSDictionary)PropertyListParser.Parse(manifestPlistPath);

            if (!_manifestPlist.TryGetValue("BackupKeyBag", out NSObject kbObj) || kbObj is not NSData kbData)
                throw new InvalidDataException("Manifest.plist does not contain a BackupKeyBag.");

            _keybag = new BackupKeybag(kbData.Bytes);
            _unlocked = _keybag.UnlockWithPassphrase(System.Text.Encoding.UTF8.GetBytes(password));
            return _unlocked;
        }

        /// <summary>
        /// Decrypts the entire backup into <paramref name="outputDir"/> as an unencrypted backup.
        /// Must be called after a successful <see cref="Unlock"/>. Returns true on success.
        /// </summary>
        public Task<bool> DecryptToFolderAsync(string outputDir, CancellationToken token, Func<bool>? checkPaused = null)
        {
            if (!_unlocked || _keybag == null || _manifestPlist == null)
                throw new InvalidOperationException("Unlock() must succeed before decrypting.");

            // All work (including the in-memory Manifest.db decrypt) runs off the UI thread.
            return Task.Run(() =>
            {
                Directory.CreateDirectory(outputDir);

                // --- 1) Decrypt Manifest.db (the backup index) ---
                LogMessage?.Invoke("Decrypting Manifest.db (backup index)...");
                string decryptedManifestDb = Path.Combine(outputDir, "Manifest.db");
                DecryptManifestDb(decryptedManifestDb);

                // --- 2) Copy/adjust the metadata plists so the folder reads as a normal backup ---
                CopyMetadataPlists(outputDir);

                // --- 3) Decrypt every file referenced by the index into the hashed layout ---
                return DecryptAllFiles(decryptedManifestDb, outputDir, token, checkPaused);
            }, token);
        }

        private void DecryptManifestDb(string destPath)
        {
            if (!_manifestPlist!.TryGetValue("ManifestKey", out NSObject mkObj) || mkObj is not NSData mkData)
                throw new InvalidDataException("Manifest.plist does not contain a ManifestKey (is the backup actually encrypted?).");

            byte[] manifestKeyFull = mkData.Bytes; // 4-byte class (LE) + 40-byte wrapped key
            uint manifestClass = BinaryPrimitives.ReadUInt32LittleEndian(manifestKeyFull.AsSpan(0, 4));
            byte[] wrapped = manifestKeyFull[4..];
            byte[] key = _keybag!.UnwrapKeyForClass(manifestClass, wrapped);

            byte[] encryptedDb = File.ReadAllBytes(Path.Combine(_backupDir, "Manifest.db"));
            byte[] decryptedDb = BackupKeybag.AesDecryptCbc(encryptedDb, key);
            // The trailing PKCS#7 padding is harmless to SQLite (it reads by page count).
            File.WriteAllBytes(destPath, decryptedDb);
        }

        private void CopyMetadataPlists(string outputDir)
        {
            foreach (var name in new[] { "Info.plist", "Status.plist" })
            {
                string src = Path.Combine(_backupDir, name);
                if (File.Exists(src))
                    File.Copy(src, Path.Combine(outputDir, name), overwrite: true);
            }

            // Write a Manifest.plist marked unencrypted so the staging folder is a valid, plain
            // backup (and won't be re-detected as encrypted if re-opened later).
            try
            {
                var manifest = (NSDictionary)PropertyListParser.Parse(Path.Combine(_backupDir, "Manifest.plist"));
                if (manifest.ContainsKey("IsEncrypted")) manifest.Remove("IsEncrypted");
                manifest.Add("IsEncrypted", new NSNumber(false));
                PropertyListParser.SaveAsBinary(manifest, new FileInfo(Path.Combine(outputDir, "Manifest.plist")));
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"Warning: could not rewrite Manifest.plist ({ex.Message}); copying original.");
                File.Copy(Path.Combine(_backupDir, "Manifest.plist"), Path.Combine(outputDir, "Manifest.plist"), overwrite: true);
            }
        }

        private bool DecryptAllFiles(string decryptedManifestDb, string outputDir, CancellationToken token, Func<bool>? checkPaused)
        {
            using var connection = new SQLiteConnection($"Data Source={decryptedManifestDb};Version=3;Read Only=True;");
            connection.Open();

            long total;
            using (var countCmd = new SQLiteCommand("SELECT COUNT(*) FROM Files WHERE flags = 1", connection))
                total = Convert.ToInt64(countCmd.ExecuteScalar());

            LogMessage?.Invoke($"Decrypting {total} files...");

            long processed = 0, success = 0, failure = 0;
            using (var cmd = new SQLiteCommand("SELECT fileID, file FROM Files WHERE flags = 1 ORDER BY domain, relativePath", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    token.ThrowIfCancellationRequested();
                    if (checkPaused != null)
                        while (checkPaused()) { token.ThrowIfCancellationRequested(); Thread.Sleep(100); }

                    string fileId = reader.GetString(0);
                    byte[]? fileBlob = reader["file"] as byte[];
                    processed++;

                    try
                    {
                        DecryptOneFile(fileId, fileBlob, outputDir, token);
                        success++;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        failure++;
                        LogMessage?.Invoke($"Failed to decrypt {fileId}: {ex.Message}");
                    }

                    if (processed % 25 == 0 || processed == total)
                    {
                        int pct = total > 0 ? (int)(processed * 100 / total) : 100;
                        ProgressReport?.Invoke(pct);
                        if (processed % 250 == 0 || processed == total)
                            LogMessage?.Invoke($"Decrypted {processed}/{total} files ({pct}%)");
                    }
                }
            }

            LogMessage?.Invoke($"Decryption complete: {success} succeeded, {failure} failed.");
            return failure == 0;
        }

        private void DecryptOneFile(string fileId, byte[]? fileBlob, string outputDir, CancellationToken token)
        {
            if (fileId.Length < 2)
                throw new InvalidDataException("Invalid fileID.");

            string srcPath = Path.Combine(_backupDir, fileId.Substring(0, 2), fileId);
            string destPath = Path.Combine(outputDir, fileId.Substring(0, 2), fileId);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

            var meta = fileBlob != null ? FileRecord.Parse(fileBlob) : null;

            // Files with no encryption key (0-byte files, etc.) are not encrypted on disk.
            if (meta?.EncryptionKey == null)
            {
                if (File.Exists(srcPath))
                    File.Copy(srcPath, destPath, overwrite: true);
                else
                    File.Create(destPath).Dispose();
                return;
            }

            if (!File.Exists(srcPath))
                throw new FileNotFoundException($"Encrypted file not found in backup: {srcPath}");

            byte[] fileKey = _keybag!.UnwrapKeyForClass(meta.ProtectionClass, meta.EncryptionKey);
            DecryptFileToDisk(srcPath, destPath, fileKey, meta.Size, token);

            if (meta.LastModified > 0)
            {
                try { File.SetLastWriteTimeUtc(destPath, DateTimeOffset.FromUnixTimeSeconds(meta.LastModified).UtcDateTime); }
                catch { /* mtime is best-effort */ }
            }
        }

        /// <summary>Streams an AES-256-CBC (zero IV) decrypt of a backup file, writing exactly Size bytes (dropping PKCS#7 padding).</summary>
        private static void DecryptFileToDisk(string srcPath, string destPath, byte[] key, long fileSize, CancellationToken token)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.IV = new byte[16];
            aes.Padding = PaddingMode.None;
            using var decryptor = aes.CreateDecryptor();

            using var inStream = File.OpenRead(srcPath);
            using var crypto = new CryptoStream(inStream, decryptor, CryptoStreamMode.Read);
            using var outStream = File.Create(destPath);

            byte[] buffer = new byte[1024 * 1024];
            long remaining = fileSize;
            while (remaining > 0)
            {
                token.ThrowIfCancellationRequested();
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = crypto.Read(buffer, 0, toRead);
                if (read <= 0) break;
                outStream.Write(buffer, 0, read);
                remaining -= read;
            }
        }

        /// <summary>Parsed metadata from a Manifest.db 'file' column (an NSKeyedArchiver binary plist).</summary>
        private sealed class FileRecord
        {
            public uint ProtectionClass;
            public long Size;
            public long LastModified;
            public byte[]? EncryptionKey;

            public static FileRecord Parse(byte[] bplist)
            {
                var root = (NSDictionary)PropertyListParser.Parse(bplist);
                var objects = ((NSArray)root["$objects"]).GetArray();
                var top = (NSDictionary)root["$top"];
                int rootIndex = UidToInt(top["root"]);
                var data = (NSDictionary)objects[rootIndex];

                var record = new FileRecord
                {
                    ProtectionClass = (uint)((NSNumber)data["ProtectionClass"]).ToInt(),
                    Size = data.TryGetValue("Size", out var size) ? ((NSNumber)size).ToLong() : 0,
                    LastModified = data.TryGetValue("LastModified", out var mt) ? ((NSNumber)mt).ToLong() : 0
                };

                if (data.TryGetValue("EncryptionKey", out var ekRef))
                {
                    var ekDict = (NSDictionary)objects[UidToInt(ekRef)];
                    byte[] nsData = ((NSData)ekDict["NS.data"]).Bytes;
                    // Strip the 4-byte class prefix, leaving the 40-byte wrapped key.
                    record.EncryptionKey = nsData[4..];
                }

                return record;
            }

            private static int UidToInt(NSObject obj)
            {
                byte[] bytes = ((UID)obj).Bytes;
                int value = 0;
                foreach (byte b in bytes)
                    value = (value << 8) | b;
                return value;
            }
        }
    }
}
