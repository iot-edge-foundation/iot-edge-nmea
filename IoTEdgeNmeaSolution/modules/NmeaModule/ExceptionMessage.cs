namespace NmeaModule
{
    using Newtonsoft.Json;

    public class ExceptionMessage
    {
        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
