using Newtonsoft.Json;
using PropertyChanged;

namespace CrossPlatformDownloadManager.Data.ViewModels;

[AddINotifyPropertyChangedInterface]
public class DaysOfWeekViewModel
{
    [JsonProperty("saturday")] public bool Saturday { get; set; }

    [JsonProperty("sunday")] public bool Sunday { get; set; }

    [JsonProperty("monday")] public bool Monday { get; set; }

    [JsonProperty("tuesday")] public bool Tuesday { get; set; }

    [JsonProperty("wednesday")] public bool Wednesday { get; set; }

    [JsonProperty("thursday")] public bool Thursday { get; set; }

    [JsonProperty("friday")] public bool Friday { get; set; }
}