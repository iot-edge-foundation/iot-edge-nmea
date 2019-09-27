namespace NmeaModule
{
    using System;
    using Newtonsoft.Json;

    public class SerialMessage
    {
        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("port")]
        public string Port { get; set; }

        [JsonProperty("timestampUtc")]
        public DateTime TimestampUtc { get; set; }
    }
}
