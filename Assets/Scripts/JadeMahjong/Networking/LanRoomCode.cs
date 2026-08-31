using System;
using System.Linq;
using System.Net;

namespace JadeMahjong.Networking
{
    public static class LanRoomCode
    {
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static string Encode(string ipv4)
        {
            if (!IPAddress.TryParse(ipv4, out var address) ||
                address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                throw new ArgumentException("A valid IPv4 address is required.", nameof(ipv4));

            var bytes = address.GetAddressBytes();
            ulong packed = ((ulong)bytes[0] << 24) |
                           ((ulong)bytes[1] << 16) |
                           ((ulong)bytes[2] << 8) |
                           bytes[3];
            var checksum = (byte)((bytes[0] * 3 + bytes[1] * 5 + bytes[2] * 7 + bytes[3] * 11) & 31);
            var value = (packed << 5) | checksum;
            var chars = new char[8];
            for (var index = chars.Length - 1; index >= 0; index--)
            {
                chars[index] = Alphabet[(int)(value & 31)];
                value >>= 5;
            }

            return new string(chars, 0, 4) + "-" + new string(chars, 4, 4);
        }

        public static bool TryDecode(string code, out string ipv4)
        {
            ipv4 = string.Empty;
            if (string.IsNullOrWhiteSpace(code))
                return false;

            var cleaned = new string(code
                .ToUpperInvariant()
                .Where(character => character != '-' && !char.IsWhiteSpace(character))
                .Select(Normalize)
                .ToArray());
            if (cleaned.Length != 8)
                return false;

            ulong value = 0;
            foreach (var character in cleaned)
            {
                var digit = Alphabet.IndexOf(character);
                if (digit < 0)
                    return false;
                value = (value << 5) | (uint)digit;
            }

            var checksum = (byte)(value & 31);
            var packed = value >> 5;
            var bytes = new[]
            {
                (byte)(packed >> 24),
                (byte)(packed >> 16),
                (byte)(packed >> 8),
                (byte)packed
            };
            var expected = (byte)((bytes[0] * 3 + bytes[1] * 5 + bytes[2] * 7 + bytes[3] * 11) & 31);
            if (checksum != expected)
                return false;

            ipv4 = new IPAddress(bytes).ToString();
            return true;
        }

        public static bool TryResolve(string codeOrAddress, out string address)
        {
            if (TryDecode(codeOrAddress, out address))
                return true;
            if (IPAddress.TryParse(codeOrAddress?.Trim(), out var parsed) &&
                parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                address = parsed.ToString();
                return true;
            }

            address = string.Empty;
            return false;
        }

        private static char Normalize(char character)
        {
            return character switch
            {
                'O' => '0',
                'I' => '1',
                'L' => '1',
                _ => character
            };
        }
    }
}
