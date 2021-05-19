using System.Text.Json.Serialization;

namespace Ceph.Airport.Models
{
    public class TokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; }
        /*

        [JsonPropertyName("expiresInSeconds")]
        public int ExpiresInSeconds { get; set; }
        */
    }
}
