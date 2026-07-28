using System.Text.Json.Serialization;
using ForgeMission.Core.Runtime;

namespace ForgeMission.Cli;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<StepMeasurement>))]
internal partial class RunMeasurementsJsonContext : JsonSerializerContext { }
