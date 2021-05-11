using System.Text.Json.Serialization;

namespace CephSked.Models
{
    class TokenResponse
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expiresInSeconds")]
        public int ExpiresInSeconds { get; set; }
    }
}
