using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ceph.Airport.Models
{
    public class TokenRequest
    {
        public string User { get; set; }
        public string Key { get; set; }
    }
}
