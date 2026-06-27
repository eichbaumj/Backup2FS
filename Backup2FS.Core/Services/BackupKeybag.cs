using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace Backup2FS.Core.Services
{
    /// <summary>
    /// iOS backup encryption primitives. This is a faithful C# port of the well-known
    /// reference implementation (jsharkey13/iphone_backup_decrypt, itself derived from the
    /// iphone-dataprotection project):
    ///   - Keybag TLV parsing
    ///   - passphrase key derivation (PBKDF2-HMAC-SHA256 then PBKDF2-HMAC-SHA1)
    ///   - RFC 3394 AES key unwrap
    ///   - AES-256-CBC decryption (zero IV) + PKCS#7 unpadding
    /// </summary>
    public sealed class BackupKeybag
    {
        private const int WrapPassphrase = 2;

        // Keybag-level attributes (e.g. SALT, ITER, DPSL, DPIC) keyed by 4-char tag.
        private readonly Dictionary<string, byte[]> _attrs = new();

        // Per-protection-class keys, keyed by the CLAS value. Each holds its raw tag bytes
        // (WPKY, WRAP, ...) plus the unwrapped "KEY" once unlockWithPassphrase succeeds.
        private readonly Dictionary<uint, Dictionary<string, byte[]>> _classKeys = new();

        public BackupKeybag(byte[] data)
        {
            ParseBinaryBlob(data);
        }

        private void ParseBinaryBlob(byte[] data)
        {
            Dictionary<string, byte[]>? currentClassKey = null;
            bool topUuidSet = false;
            bool topWrapSet = false;

            foreach (var (tag, value) in LoopTlvBlocks(data))
            {
                switch (tag)
                {
                    case "TYPE":
                        // keybag type — not needed for decryption
                        break;
                    case "UUID" when !topUuidSet:
                        topUuidSet = true;
                        break;
                    case "WRAP" when !topWrapSet:
                        topWrapSet = true;
                        break;
                    case "UUID":
                        // A subsequent UUID starts a new class-key block.
                        if (currentClassKey != null && currentClassKey.TryGetValue("CLAS", out var prevClas))
                            _classKeys[BinaryPrimitives.ReadUInt32BigEndian(Pad4(prevClas))] = currentClassKey;
                        currentClassKey = new Dictionary<string, byte[]> { ["UUID"] = value };
                        break;
                    case "CLAS":
                    case "WRAP":
                    case "WPKY":
                    case "KTYP":
                    case "PBKY":
                        if (currentClassKey != null)
                            currentClassKey[tag] = value;
                        break;
                    default:
                        _attrs[tag] = value;
                        break;
                }
            }

            if (currentClassKey != null && currentClassKey.TryGetValue("CLAS", out var lastClas))
                _classKeys[BinaryPrimitives.ReadUInt32BigEndian(Pad4(lastClas))] = currentClassKey;
        }

        /// <summary>
        /// Unlock the keybag's class keys with the iTunes backup passphrase.
        /// Returns false if the passphrase is incorrect (an unwrap fails its integrity check).
        /// </summary>
        public bool UnlockWithPassphrase(byte[] passphrase)
        {
            byte[] dpsl = _attrs["DPSL"];
            int dpic = (int)BinaryPrimitives.ReadUInt32BigEndian(Pad4(_attrs["DPIC"]));
            byte[] salt = _attrs["SALT"];
            int iter = (int)BinaryPrimitives.ReadUInt32BigEndian(Pad4(_attrs["ITER"]));

            // iOS 10.2+ double round: SHA-256 then SHA-1.
            byte[] round1 = Rfc2898DeriveBytes.Pbkdf2(passphrase, dpsl, dpic, HashAlgorithmName.SHA256, 32);
            byte[] passphraseKey = Rfc2898DeriveBytes.Pbkdf2(round1, salt, iter, HashAlgorithmName.SHA1, 32);

            bool anyUnwrapped = false;
            foreach (var classKey in _classKeys.Values)
            {
                if (!classKey.TryGetValue("WPKY", out var wpky))
                    continue;
                uint wrap = classKey.TryGetValue("WRAP", out var w) ? BinaryPrimitives.ReadUInt32BigEndian(Pad4(w)) : 0;
                if ((wrap & WrapPassphrase) != 0)
                {
                    byte[]? unwrapped = AesUnwrap(passphraseKey, wpky);
                    if (unwrapped == null)
                        return false; // wrong passphrase
                    classKey["KEY"] = unwrapped;
                    anyUnwrapped = true;
                }
            }
            return anyUnwrapped;
        }

        /// <summary>
        /// Unwrap a persistent (wrapped) file/manifest key using the class key for the given
        /// protection class. The persistent key is the 40-byte wrapped key from the file plist.
        /// </summary>
        public byte[] UnwrapKeyForClass(uint protectionClass, byte[] persistentKey)
        {
            if (!_classKeys.TryGetValue(protectionClass, out var classKey) || !classKey.TryGetValue("KEY", out var ck))
                throw new InvalidOperationException($"No unlocked class key for protection class {protectionClass}.");
            if (persistentKey.Length != 0x28)
                throw new ArgumentException("Invalid wrapped key length (expected 40 bytes).", nameof(persistentKey));
            byte[]? result = AesUnwrap(ck, persistentKey);
            if (result == null)
                throw new CryptographicException("Failed to unwrap file key (corrupt backup or wrong class key).");
            return result;
        }

        private static IEnumerable<(string tag, byte[] value)> LoopTlvBlocks(byte[] blob)
        {
            int i = 0;
            while (i + 8 <= blob.Length)
            {
                string tag = System.Text.Encoding.ASCII.GetString(blob, i, 4);
                int length = (int)BinaryPrimitives.ReadUInt32BigEndian(blob.AsSpan(i + 4, 4));
                if (i + 8 + length > blob.Length) break;
                var value = new byte[length];
                Array.Copy(blob, i + 8, value, 0, length);
                yield return (tag, value);
                i += 8 + length;
            }
        }

        // Right-align bytes into a 4-byte big-endian buffer (values may be exactly 4 bytes).
        private static byte[] Pad4(byte[] b)
        {
            if (b.Length == 4) return b;
            var r = new byte[4];
            int copy = Math.Min(4, b.Length);
            Array.Copy(b, b.Length - copy, r, 4 - copy, copy);
            return r;
        }

        /// <summary>RFC 3394 AES key unwrap. Returns null if the integrity check (A == 0xA6...) fails.</summary>
        private static byte[]? AesUnwrap(byte[] kek, byte[] wrapped)
        {
            int blocks = wrapped.Length / 8;
            ulong[] c = new ulong[blocks];
            for (int i = 0; i < blocks; i++)
                c[i] = BinaryPrimitives.ReadUInt64BigEndian(wrapped.AsSpan(i * 8, 8));

            int n = blocks - 1;
            ulong[] r = new ulong[n + 1];
            ulong a = c[0];
            for (int i = 1; i <= n; i++)
                r[i] = c[i];

            using var aes = Aes.Create();
            aes.Key = kek;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            using var decryptor = aes.CreateDecryptor();

            byte[] block = new byte[16];
            byte[] outBlock = new byte[16];
            for (int j = 5; j >= 0; j--)
            {
                for (int i = n; i >= 1; i--)
                {
                    BinaryPrimitives.WriteUInt64BigEndian(block.AsSpan(0, 8), a ^ (ulong)(n * j + i));
                    BinaryPrimitives.WriteUInt64BigEndian(block.AsSpan(8, 8), r[i]);
                    decryptor.TransformBlock(block, 0, 16, outBlock, 0);
                    a = BinaryPrimitives.ReadUInt64BigEndian(outBlock.AsSpan(0, 8));
                    r[i] = BinaryPrimitives.ReadUInt64BigEndian(outBlock.AsSpan(8, 8));
                }
            }

            if (a != 0xa6a6a6a6a6a6a6a6UL)
                return null;

            byte[] res = new byte[n * 8];
            for (int i = 1; i <= n; i++)
                BinaryPrimitives.WriteUInt64BigEndian(res.AsSpan((i - 1) * 8, 8), r[i]);
            return res;
        }

        /// <summary>AES-256-CBC decrypt with a zero IV and no padding (full buffer).</summary>
        public static byte[] AesDecryptCbc(byte[] data, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.IV = new byte[16];
            aes.Padding = PaddingMode.None;
            using var decryptor = aes.CreateDecryptor();
            int usable = data.Length - (data.Length % 16);
            return decryptor.TransformFinalBlock(data, 0, usable);
        }
    }
}
