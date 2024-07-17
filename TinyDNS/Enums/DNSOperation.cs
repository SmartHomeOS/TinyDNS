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
    public enum DNSOperation : byte
    {
        Query = 0,
        [Obsolete]
        IQuery = 1,
        Status = 2,
        Unassigned = 3,
        Notify = 4,
        Update = 5,
        DNSStatefulOps = 6
    }
}
