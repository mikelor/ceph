using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ceph.Airport.Models
{
    public class Airport
    {
        public string Code { get; set; }
        public int MinHour { get; set; }
        public int MaxHour { get; set; }
    }
}
