using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeMission.Cli.Docker;

// ── STJ source-gen context ─────────────────────────────────────────────────

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DockerContainer[]))]
[JsonSerializable(typeof(DockerCreateContainerRequest))]
[JsonSerializable(typeof(DockerCreateContainerResponse))]
[JsonSerializable(typeof(DockerCreateNetworkRequest))]
[JsonSerializable(typeof(DockerVersionResponse))]
[JsonSerializable(typeof(DockerProgressEvent))]
[JsonSerializable(typeof(DockerHostConfig))]
[JsonSerializable(typeof(DockerPortBinding[]))]
[JsonSerializable(typeof(Dictionary<string, DockerPortBinding[]>))]
[JsonSerializable(typeof(Dictionary<string, DockerEmptyObject>))]
internal partial class DockerJsonContext : JsonSerializerContext { }

internal sealed class DockerContainer
{
    [JsonPropertyName("Id")]    public string Id    { get; set; } = "";
    [JsonPropertyName("Names")] public string[] Names { get; set; } = [];
}

internal sealed class DockerCreateContainerRequest
{
    [JsonPropertyName("Image")]        public string Image { get; set; } = "";
    [JsonPropertyName("Cmd")]          public string[]? Cmd { get; set; }
    [JsonPropertyName("Env")]          public string[]? Env { get; set; }
    [JsonPropertyName("ExposedPorts")] public Dictionary<string, DockerEmptyObject>? ExposedPorts { get; set; }
    [JsonPropertyName("HostConfig")]   public DockerHostConfig? HostConfig { get; set; }
}

internal sealed class DockerHostConfig
{
    [JsonPropertyName("Binds")]        public string[]? Binds { get; set; }
    [JsonPropertyName("PortBindings")] public Dictionary<string, DockerPortBinding[]>? PortBindings { get; set; }
    [JsonPropertyName("NetworkMode")]  public string? NetworkMode { get; set; }
}

internal sealed class DockerPortBinding
{
    [JsonPropertyName("HostPort")] public string HostPort { get; set; } = "";
}

internal sealed class DockerEmptyObject { }

internal sealed class DockerCreateContainerResponse
{
    [JsonPropertyName("Id")]       public string Id       { get; set; } = "";
    [JsonPropertyName("Warnings")] public string[]? Warnings { get; set; }
}

internal sealed class DockerCreateNetworkRequest
{
    [JsonPropertyName("Name")]   public string Name   { get; set; } = "";
    [JsonPropertyName("Driver")] public string Driver { get; set; } = "bridge";
}

internal sealed class DockerVersionResponse
{
    [JsonPropertyName("Version")] public string Version { get; set; } = "";
    [JsonPropertyName("Os")]      public string Os      { get; set; } = "";
    [JsonPropertyName("Arch")]    public string Arch    { get; set; } = "";
}

internal sealed class DockerProgressEvent
{
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("error")]  public string? Error { get; set; }
}

// ── Docker Engine API client (Unix socket) ─────────────────────────────────

public static class DockerCli
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var sockPath = "/var/run/docker.sock";
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, ct) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(sockPath), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
        };
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }

    // ── Prereq helper ──────────────────────────────────────────────────────

    internal static async Task<(bool Ok, string Detail)> GetVersionAsync()
    {
        try
        {
            var resp = await Http.GetAsync("/v1.41/version");
            if (!resp.IsSuccessStatusCode)
                return (false, "not running — start Docker Desktop");
            var json = await resp.Content.ReadAsStringAsync();
            var v = JsonSerializer.Deserialize(json, DockerJsonContext.Default.DockerVersionResponse);
            return (true, $"Docker Engine {v?.Version} ({v?.Os}/{v?.Arch})");
        }
        catch
        {
            return (false, "not running — start Docker Desktop");
        }
    }

    // ── Image operations ───────────────────────────────────────────────────

    public static async Task<bool> IsImagePresentAsync(string image)
    {
        var resp = await Http.GetAsync($"/v1.41/images/{Uri.EscapeDataString(image)}/json");
        return resp.StatusCode == HttpStatusCode.OK;
    }

    public static async Task PullImageAsync(string image)
    {
        var colonIdx = image.LastIndexOf(':');
        var fromImage = colonIdx >= 0 ? image[..colonIdx] : image;
        var tag       = colonIdx >= 0 ? image[(colonIdx + 1)..] : "latest";

        var resp = await Http.PostAsync(
            $"/v1.41/images/create?fromImage={Uri.EscapeDataString(fromImage)}&tag={Uri.EscapeDataString(tag)}",
            null);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"docker pull failed: {err}");
        }

        // Docker streams NDJSON progress events — drain and print status lines
        using var stream = await resp.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var evt = JsonSerializer.Deserialize(line, DockerJsonContext.Default.DockerProgressEvent);
            if (evt?.Error is not null)
                throw new InvalidOperationException($"docker pull error: {evt.Error}");
            if (!string.IsNullOrEmpty(evt?.Status))
                Console.WriteLine(evt.Status);
        }
    }

    // ── Network operations ─────────────────────────────────────────────────

    public static async Task<bool> NetworkExistsAsync(string name)
    {
        var resp = await Http.GetAsync($"/v1.41/networks/{name}");
        return resp.StatusCode == HttpStatusCode.OK;
    }

    public static async Task EnsureNetworkAsync(string name)
    {
        if (await NetworkExistsAsync(name)) return;
        var body = JsonSerializer.Serialize(
            new DockerCreateNetworkRequest { Name = name, Driver = "bridge" },
            DockerJsonContext.Default.DockerCreateNetworkRequest);
        var resp = await Http.PostAsync("/v1.41/networks/create",
            new StringContent(body, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();
    }

    // ── Container operations ───────────────────────────────────────────────

    public static async Task<bool> IsContainerRunningAsync(string name)
    {
        var f = Uri.EscapeDataString($"{{\"name\":[\"^/{name}$\"],\"status\":[\"running\"]}}");
        var resp = await Http.GetAsync($"/v1.41/containers/json?filters={f}");
        if (!resp.IsSuccessStatusCode) return false;
        var containers = JsonSerializer.Deserialize(
            await resp.Content.ReadAsStringAsync(),
            DockerJsonContext.Default.DockerContainerArray);
        return containers?.Length > 0;
    }

    public static async Task<bool> ContainerExistsAsync(string name)
    {
        var f = Uri.EscapeDataString($"{{\"name\":[\"^/{name}$\"]}}");
        var resp = await Http.GetAsync($"/v1.41/containers/json?all=true&filters={f}");
        if (!resp.IsSuccessStatusCode) return false;
        var containers = JsonSerializer.Deserialize(
            await resp.Content.ReadAsStringAsync(),
            DockerJsonContext.Default.DockerContainerArray);
        return containers?.Length > 0;
    }

    public static async Task RunContainerAsync(
        string name,
        string image,
        string[] cmd,
        string[] env,
        string[] binds,
        int hostPort,
        int containerPort,
        string network)
    {
        var portKey = $"{containerPort}/tcp";
        var request = new DockerCreateContainerRequest
        {
            Image = image,
            Cmd   = cmd.Length > 0 ? cmd : null,
            Env   = env.Length  > 0 ? env : null,
            ExposedPorts = new Dictionary<string, DockerEmptyObject> { [portKey] = new() },
            HostConfig = new DockerHostConfig
            {
                Binds       = binds.Length > 0 ? binds : null,
                NetworkMode = network,
                PortBindings = new Dictionary<string, DockerPortBinding[]>
                {
                    [portKey] = [new DockerPortBinding { HostPort = hostPort.ToString() }]
                }
            }
        };

        var body = JsonSerializer.Serialize(request, DockerJsonContext.Default.DockerCreateContainerRequest);
        var createResp = await Http.PostAsync(
            $"/v1.41/containers/create?name={name}",
            new StringContent(body, Encoding.UTF8, "application/json"));

        if (!createResp.IsSuccessStatusCode)
        {
            var err = await createResp.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create container '{name}': {err}");
        }

        var created = JsonSerializer.Deserialize(
            await createResp.Content.ReadAsStringAsync(),
            DockerJsonContext.Default.DockerCreateContainerResponse);
        var id = created?.Id ?? throw new InvalidOperationException("No container ID in response");

        var startResp = await Http.PostAsync($"/v1.41/containers/{id}/start", null);
        if (!startResp.IsSuccessStatusCode)
        {
            var err = await startResp.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to start container '{name}': {err}");
        }
    }

    public static async Task StopAndRemoveAsync(string name)
    {
        var f = Uri.EscapeDataString($"{{\"name\":[\"^/{name}$\"]}}");
        var resp = await Http.GetAsync($"/v1.41/containers/json?all=true&filters={f}");
        if (!resp.IsSuccessStatusCode) return;
        var containers = JsonSerializer.Deserialize(
            await resp.Content.ReadAsStringAsync(),
            DockerJsonContext.Default.DockerContainerArray);
        if (containers is null || containers.Length == 0) return;

        var id = containers[0].Id;
        await Http.PostAsync($"/v1.41/containers/{id}/stop", null);
        await Http.DeleteAsync($"/v1.41/containers/{id}");
    }
}
