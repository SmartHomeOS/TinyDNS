// TinyDNS Copyright (C) 2024 
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System.Globalization;
using System.Net;
using System.Text;

namespace TinyDNS
{
    public sealed class DomainParser
    {
        private const byte ROOT = 0x0;

        public static List<string> Read(Span<byte> bytes, ref int position, int iteration = 0, bool partialDomain = false)
        {
            iteration++;
            if (iteration >= 32)
                throw new InvalidDataException("Maximum pointer recursion exceeded");
            try
            {
                List<string> labels = [];
                byte length = bytes[position++];
                while (length != ROOT)
                {
                    if ((length & 0xC0) == 0xC0)
                    {
                        int ptr = bytes[position++];
                        ptr |= (int)((length & 0x3F) << 8);
                        if (ptr >= position)
                            throw new InvalidDataException("Forward Pointer");
                        labels.AddRange(Read(bytes.Slice(0, position), ref ptr, iteration, false));
                        break;
                    }
                    else if ((length & 0xC0) == 0x00)
                    {
                        labels.Add(Encoding.UTF8.GetString(bytes.Slice(position, length)));
                        position += length;
                    }
                    else
                        throw new InvalidDataException("Invalid Length Specified");
                    if ((position + 1) >  bytes.Length)
                    {
                        if (partialDomain)
                            return labels;
                        throw new InvalidDataException("Domain was not fully qualified");
                    }
                    length = bytes[position++];
                }
                return labels;
            }
            catch (Exception e) when (e is IndexOutOfRangeException || e is ArgumentOutOfRangeException)
            {
                throw new InvalidDataException("Unable to parse domain");
            }
        }

        public static void Write(List<string> labels, Span<byte> buffer, ref int pos)
        {
            foreach (string label in labels)
            {
                StringBuilder ret = new StringBuilder();
                foreach (char c in label)
                {
                    if (c > 0x1F && c != 0x7E)
                        ret.Append(c);
                }
                byte[] bytes = Encoding.UTF8.GetBytes(ret.ToString());
                int len = Math.Min(bytes.Length, 63);
                buffer[pos++] = (byte)len;
                bytes.AsSpan(0, len).CopyTo(buffer.Slice(pos));
                pos += len;
            }
            buffer[pos++] = ROOT;
        }

        public static List<string> Parse(string domain)
        {
            List<string> labels = new List<string>();
            StringBuilder label = new StringBuilder();
            ReadOnlySpan<char> domainSpan = domain.AsSpan();
            for (int i = 0; i < domain.Length; i++)
            {
                if (domain[i] == '\\')
                {
                    //Escaped char follows
                    if (char.IsAsciiHexDigit(domain[++i]))
                        label.Append((char)int.Parse(domainSpan.Slice(i++, 2), NumberStyles.HexNumber)); //2 digit hex code
                    else
                        label.Append(domain[i]); //single character
                }
                else if (domain[i] == '.')
                {
                    labels.Add(label.ToString());
                    label.Clear();
                }
                else
                    label.Append(domain[i]);
            }
            if (label.Length > 0)
                labels.Add(label.ToString());
            return labels;
        }

        internal static List<string> FromIP(IPAddress address)
        {
            return FromIP(address.GetAddressBytes());
        }

        internal static List<string> FromIP(byte[] address)
        {
            List<string> host;
            if (address.Length == 4)
            {
                host = new List<string>(6);
                for (int i = address.Length - 1; i >= 0; i--)
                    host.Add(address[i].ToString());

                host.Add("in-addr");
                host.Add("arpa");
            }
            else
            {
                host = new List<string>(34);
                for (int i = address.Length - 1; i >= 0; i--)
                {
                    string hex = address[i].ToString("x2");
                    host.Add(hex.Substring(1, 1));
                    host.Add(hex.Substring(0, 1));
                }
                host.Add("IP6");
                host.Add("ARPA");
            }
            return host;
        }
    }
}
