using System.Text.Json.Serialization;

namespace PhonePadBridge.WebBridge;

public sealed class ControllerState
{
    [JsonPropertyName("type")] public string Type { get; set; } = "state";
    [JsonPropertyName("seq")] public ulong Seq { get; set; }
    [JsonPropertyName("timestamp")] public double Timestamp { get; set; }

    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("lx")] public float Lx { get; set; }
    [JsonPropertyName("ly")] public float Ly { get; set; }
    [JsonPropertyName("rx")] public float Rx { get; set; }
    [JsonPropertyName("ry")] public float Ry { get; set; }
    [JsonPropertyName("lt")] public float Lt { get; set; }
    [JsonPropertyName("rt")] public float Rt { get; set; }

    [JsonPropertyName("a")] public bool A { get; set; }
    [JsonPropertyName("b")] public bool B { get; set; }
    [JsonPropertyName("x")] public bool X { get; set; }
    [JsonPropertyName("y")] public bool Y { get; set; }

    [JsonPropertyName("lb")] public bool Lb { get; set; }
    [JsonPropertyName("rb")] public bool Rb { get; set; }
    [JsonPropertyName("back")] public bool Back { get; set; }
    [JsonPropertyName("start")] public bool Start { get; set; }
    [JsonPropertyName("ls")] public bool Ls { get; set; }
    [JsonPropertyName("rs")] public bool Rs { get; set; }

    [JsonPropertyName("dpadUp")] public bool DpadUp { get; set; }
    [JsonPropertyName("dpadDown")] public bool DpadDown { get; set; }
    [JsonPropertyName("dpadLeft")] public bool DpadLeft { get; set; }
    [JsonPropertyName("dpadRight")] public bool DpadRight { get; set; }
}
