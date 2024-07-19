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
    public enum DNSStatus : byte
    {
        /// <summary>
        /// No Error
        /// </summary>
        NoError = 0,
        /// <summary>
        /// Format Error
        /// </summary>
        FormatError = 1,
        /// <summary>
        /// Server Failure
        /// </summary>
        ServerFailure = 2,
        /// <summary>
        /// Non-Existent Domain
        /// </summary>
        NameError = 3,
        /// <summary>
        /// Not Implemented
        /// </summary>
        NotImplemented = 4,
        /// <summary>
        /// Query Refused
        /// </summary>
        Refused = 5,
        /// <summary>
        /// Name Exists when it should not	
        /// </summary>
        YXDomain = 6,
        /// <summary>
        /// RR Set Exists when it should not	
        /// </summary>
        YXRRSet = 7,
        /// <summary>
        /// RR Set that should exist does not	
        /// </summary>
        NXRRSet = 8,
        /// <summary>
        /// Server Not Authoritative for zone
        /// </summary>
        NotAuth = 9,
        /// <summary>
        /// Name not contained in zone
        /// </summary>
        NotZone = 10,
        /// <summary>
        /// DSO-TYPE Not Implemented
        /// </summary>
        DSOTYPENI = 11,

    }
}
