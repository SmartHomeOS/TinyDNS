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

namespace TinyDNS.Enums
{
    public enum DNSRecordType : ushort
    {
        None = 0x00,
        A = 0x01,
        NS = 0x02,
        CNAME = 0x05,
        SOA = 0x06,
        PTR = 0x0C,
        HINFO = 0x0D,
        MX = 0x0F,
        TXT = 0x10,
        AAAA = 0x1C,
        SRV = 0x21,
        DNAME = 0x27,
        ANY = 0xFF,
    }
}
