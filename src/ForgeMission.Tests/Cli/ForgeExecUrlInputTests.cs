using System.Reflection;

namespace ForgeMission.Tests.Cli;

public sealed class ForgeExecUrlInputTests
{
    private static readonly Type ForgeExec = LoadCliType("ForgeMission.Cli.ForgeExec");
    private static readonly Type MissionArtifactDto = LoadCliType("ForgeMission.Cli.MissionArtifactDto");

    [Theory]
    [InlineData("https://example.test/scan.jpg?sig=abc", true)]
    [InlineData("http://example.test/scan.jpg", true)]
    [InlineData("ftp://example.test/scan.jpg", false)]
    [InlineData("s3://bucket/scan.jpg", false)]
    [InlineData("./scan.jpg", false)]
    [InlineData("C:\\temp\\scan.jpg", false)]
    public void IsHttpUrl_accepts_only_absolute_http_and_https(string input, bool expected)
    {
        Assert.Equal(expected, IsHttpUrl(input));
    }

    [Fact]
    public void InputPathForOutputInference_uses_url_local_path_not_query_string()
    {
        var path = Invoke<string>(
            "InputPathForOutputInference",
            "https://mydemosa.blob.core.windows.net/samples/scan.jpg?sv=2026&sig=secret");

        Assert.Equal("scan.jpg", path);
    }

    [Fact]
    public void InferOutputPath_uses_url_file_name()
    {
        var output = Invoke<string>(
            "InferOutputPath",
            "https://mydemosa.blob.core.windows.net/samples/scan.jpg?sv=2026&sig=secret",
            null,
            Artifact("text/plain"));

        Assert.Equal("scan.txt", output);
    }

    [Fact]
    public void InferOutputPath_keeps_pdf_ocr_suffix_for_url_pdf_to_pdf()
    {
        var output = Invoke<string>(
            "InferOutputPath",
            "https://example.test/files/scan.pdf?sig=secret",
            "pdf",
            Artifact("application/pdf"));

        Assert.Equal("scan.ocr.pdf", output);
    }

    [Fact]
    public void InferDownloadFileName_prefers_original_url_file_name()
    {
        var fileName = Invoke<string>(
            "InferDownloadFileName",
            new Uri("https://example.test/files/scan.jpg?sig=secret"),
            new Uri("https://cdn.example.test/redirected/final.png"),
            "image/png");

        Assert.Equal("scan.jpg", fileName);
    }

    [Fact]
    public void InferDownloadFileName_uses_redirect_file_name_when_original_has_none()
    {
        var fileName = Invoke<string>(
            "InferDownloadFileName",
            new Uri("https://example.test/download?sig=secret"),
            new Uri("https://cdn.example.test/files/final.png"),
            "image/jpeg");

        Assert.Equal("final.png", fileName);
    }

    [Fact]
    public void InferDownloadFileName_falls_back_to_content_type_extension()
    {
        var fileName = Invoke<string>(
            "InferDownloadFileName",
            new Uri("https://example.test/download?sig=secret"),
            null,
            "image/jpeg");

        Assert.Equal("download.jpg", fileName);
    }

    [Fact]
    public void ContentTypeForDownload_prefers_known_extension()
    {
        var contentType = Invoke<string>("ContentTypeForDownload", "scan.jpg", "application/octet-stream");

        Assert.Equal("image/jpeg", contentType);
    }

    [Fact]
    public void ContentTypeForDownload_uses_response_content_type_for_unknown_extension()
    {
        var contentType = Invoke<string>("ContentTypeForDownload", "scan.weird", "image/png");

        Assert.Equal("image/png", contentType);
    }

    private static bool IsHttpUrl(string input)
    {
        var method = Method("IsHttpUrl");
        object?[] args = [input, null];
        return (bool)method.Invoke(null, args)!;
    }

    private static object Artifact(string contentType)
    {
        var artifact = Activator.CreateInstance(MissionArtifactDto, nonPublic: true)!;
        MissionArtifactDto.GetProperty("ContentType")!.SetValue(artifact, contentType);
        return artifact;
    }

    private static T Invoke<T>(string methodName, params object?[] args) =>
        (T)Method(methodName).Invoke(null, args)!;

    private static MethodInfo Method(string name) =>
        ForgeExec.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(ForgeExec.FullName, name);

    private static Type LoadCliType(string typeName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "ForgeMission.Cli",
                "bin",
                "Debug",
                "net10.0",
                "forge.dll");
            if (File.Exists(candidate))
                return Assembly.LoadFrom(candidate).GetType(typeName, throwOnError: true)!;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate built forge.dll for CLI reflection tests.");
    }
}
