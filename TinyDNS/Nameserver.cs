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

using System.Net;

namespace TinyDNS
{
    public struct Nameserver
    {
        public Nameserver(IPAddress nameserver)
        {
            Address = nameserver;
            DNSSuffix = string.Empty;
        }

        public Nameserver(IPAddress nameserver, bool supportsDoh) : this(nameserver)
        {
            this.SupportsDoH = supportsDoh;
            DNSSuffix = string.Empty;
        }

        public Nameserver(IPAddress nameserver, string suffix) : this(nameserver)
        {
            this.DNSSuffix = suffix;
        }

        public string DNSSuffix { get; set; }
        public bool? SupportsDoH { get; set; }
        public IPAddress Address { get; }
    }
}
