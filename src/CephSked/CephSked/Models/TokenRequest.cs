using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CephSked.Models
{
    class TokenRequest
    {
        public string User { get; set; }
        public string Key { get; set; }
    }
}
