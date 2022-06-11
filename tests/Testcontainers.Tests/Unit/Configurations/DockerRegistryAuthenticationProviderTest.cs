﻿namespace DotNet.Testcontainers.Tests.Unit
{
  using System;
  using System.IO;
  using System.Runtime.InteropServices;
  using System.Text.Json;
  using DotNet.Testcontainers.Builders;
  using DotNet.Testcontainers.Configurations;
  using DotNet.Testcontainers.Images;
  using Xunit;

  public sealed class DockerRegistryAuthenticationProviderTest
  {
    private const string DockerRegistry = "https://index.docker.io/v1/";

    [Theory]
    [InlineData("baz/foo/bar:1.0.0", null)]
    [InlineData("baz/foo/bar", null)]
    [InlineData("baz/foo/bar:latest", null)]
    [InlineData("foo/bar:1.0.0", null)]
    [InlineData("foo/bar", null)]
    [InlineData("foo/bar:latest", null)]
    [InlineData("bar:1.0.0", null)]
    [InlineData("bar:latest", null)]
    [InlineData("myregistry.azurecr.io/baz/foo/bar:1.0.0", "myregistry.azurecr.io")]
    [InlineData("myregistry.azurecr.io/baz/foo/bar", "myregistry.azurecr.io")]
    [InlineData("myregistry.azurecr.io/baz/foo/bar:latest", "myregistry.azurecr.io")]
    [InlineData("myregistry.azurecr.io/bar:1.0.0", "myregistry.azurecr.io")]
    [InlineData("fedora/httpd:version1.0.test", null)]
    [InlineData("fedora/httpd:version1.0", null)]
    [InlineData("myregistryhost:5000/fedora/httpd:version1.0", "myregistryhost:5000")]
    [InlineData("myregistryhost:5000/httpd:version1.0", "myregistryhost:5000")]
    [InlineData("baz/.foo/bar:1.0.0", null)]
    [InlineData("baz/:foo/bar:1.0.0", null)]
    [InlineData("myregistry.azurecr.io/baz.foo/bar:1.0.0", "myregistry.azurecr.io")]
    [InlineData("myregistry.azurecr.io/baz:foo/bar:1.0.0", "myregistry.azurecr.io")]
    public void GetHostnameFromDockerImage(string dockerImageName, string hostname)
    {
      IDockerImage image = new DockerImage(dockerImageName);
      Assert.Equal(hostname, image.GetHostname());
    }

    [Theory]
    [InlineData("", "docker", "stable")]
    [InlineData("fedora", "httpd", "1.0")]
    [InlineData("foo/bar", "baz", "1.0.0")]
    public void GetHostnameFromHubImageNamePrefix(string repository, string name, string tag)
    {
      const string hubImageNamePrefix = "myregistry.azurecr.io";
      IDockerImage image = new DockerImage(repository, name, tag, hubImageNamePrefix);
      Assert.Equal(hubImageNamePrefix, image.GetHostname());
    }

    [Fact]
    public void ShouldGetDefaultDockerRegistryAuthenticationConfiguration()
    {
      var authenticationProvider = new DockerRegistryAuthenticationProvider("/tmp/docker.config", TestcontainersSettings.Logger);
      Assert.Equal(default(DockerRegistryAuthenticationConfiguration), authenticationProvider.GetAuthConfig(DockerRegistry));
    }

    public sealed class Base64ProviderTest
    {
      [Theory]
      [InlineData("{}", false)]
      [InlineData("{\"auths\":{}}", false)]
      [InlineData("{\"auths\":{\"ghcr.io\":{}}}", true)]
      [InlineData("{\"auths\":{\"" + DockerRegistry + "\":{}}}", true)]
      [InlineData("{\"auths\":{\"" + DockerRegistry + "\":{\"auth\":null}}}", true)]
      [InlineData("{\"auths\":{\"" + DockerRegistry + "\":{\"auth\":\"\"}}}", true)]
      [InlineData("{\"auths\":{\"" + DockerRegistry + "\":{\"auth\":\"dXNlcm5hbWU=\"}}}", true)]
      public void ShouldGetNull(string jsonDocument, bool isApplicable)
      {
        // Given
        var jsonElement = JsonDocument.Parse(jsonDocument).RootElement;

        // When
        var authenticationProvider = new Base64Provider(jsonElement, TestcontainersSettings.Logger);
        var authConfig = authenticationProvider.GetAuthConfig(DockerRegistry);

        // Then
        Assert.Equal(isApplicable, authenticationProvider.IsApplicable(DockerRegistry));
        Assert.Null(authConfig);
      }

      [Fact]
      public void ShouldGetAuthConfig()
      {
        // Given
        const string jsonDocument = "{\"auths\":{\"" + DockerRegistry + "\":{\"auth\":\"dXNlcm5hbWU6cGFzc3dvcmQ=\"}}}";
        var jsonElement = JsonDocument.Parse(jsonDocument).RootElement;

        // When
        var authenticationProvider = new Base64Provider(jsonElement, TestcontainersSettings.Logger);
        var authConfig = authenticationProvider.GetAuthConfig(DockerRegistry);

        // Then
        Assert.True(authenticationProvider.IsApplicable(DockerRegistry));
        Assert.NotNull(authConfig);
        Assert.Equal(DockerRegistry, authConfig.RegistryEndpoint);
        Assert.Equal("username", authConfig.Username);
        Assert.Equal("password", authConfig.Password);
      }
    }

    public sealed class CredsStoreProviderTest
    {
      [Theory]
      [InlineData("{}", false)]
      [InlineData("{\"credsStore\":null}", false)]
      [InlineData("{\"credsStore\":\"\"}", false)]
      public void ShouldGetNull(string jsonDocument, bool isApplicable)
      {
        // Given
        var jsonElement = JsonDocument.Parse(jsonDocument).RootElement;

        // When
        var authenticationProvider = new CredsStoreProvider(jsonElement, TestcontainersSettings.Logger);
        var authConfig = authenticationProvider.GetAuthConfig(DockerRegistry);

        // Then
        Assert.Equal(isApplicable, authenticationProvider.IsApplicable(DockerRegistry));
        Assert.Null(authConfig);
      }

#pragma warning disable xUnit1004

      [Fact(Skip = "The pipeline has no configured credential store (maybe we can use the Windows tests in the future).")]
#pragma warning restore xUnit1004
      public void ShouldGetAuthConfig()
      {
        // Given
        const string jsonDocument = "{\"credsStore\":\"desktop\"}";
        var jsonElement = JsonDocument.Parse(jsonDocument).RootElement;

        // When
        var authenticationProvider = new CredsStoreProvider(jsonElement, TestcontainersSettings.Logger);
        var authConfig = authenticationProvider.GetAuthConfig(DockerRegistry);

        // Then
        Assert.True(authenticationProvider.IsApplicable(DockerRegistry));
        Assert.NotNull(authConfig);
        Assert.Equal(DockerRegistry, authConfig.RegistryEndpoint);
        Assert.Equal("username", authConfig.Username);
        Assert.Equal("password", authConfig.Password);
      }
    }

    public sealed class CredsHelperProviderTest
    {
      [Theory]
      [InlineData("{}", false)]
      [InlineData("{\"credHelpers\":null}", false)]
      [InlineData("{\"credHelpers\":{\"" + DockerRegistry + "\":null}}", false)]
      [InlineData("{\"credHelpers\":{\"registry2\":\"script.bat\"}}", false)]
      public void ShouldGetNull(string jsonDocument, bool isApplicable)
      {
        // Given
        var jsonElement = JsonDocument.Parse(jsonDocument).RootElement;

        // When
        var authenticationProvider = new CredsHelperProvider(jsonElement, TestcontainersSettings.Logger);
        var authConfig = authenticationProvider.GetAuthConfig(DockerRegistry);

        // Then
        Assert.Equal(isApplicable, authenticationProvider.IsApplicable(DockerRegistry));
        Assert.Null(authConfig);
      }

      [Theory]
      [InlineData("password", "username", "password", null)]
      [InlineData("token", null, null, "identitytoken")]
      public void ShouldGetAuthConfig(string credHelperName, string expectedUsername, string expectedPassword, string expectedIdentityToken)
      {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ";" : ":";
        var credHelpersPath = Path.Combine(Environment.CurrentDirectory, "Assets", "credHelpers");
        Environment.SetEnvironmentVariable("PATH", string.Join(pathSeparator, credHelpersPath, path));
        var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".bat" : ".sh";
        var credHelperScriptSuffix = $"{credHelperName}{extension}";

        // Given
        var jsonDocument = "{\"credHelpers\":{\"" + DockerRegistry + "\":\"" + credHelperScriptSuffix + "\"}}";
        var jsonElement = JsonDocument.Parse(jsonDocument).RootElement;

        // When
        var authenticationProvider = new CredsHelperProvider(jsonElement, TestcontainersSettings.Logger);
        var authConfig = authenticationProvider.GetAuthConfig(DockerRegistry);

        // Then
        Assert.True(authenticationProvider.IsApplicable(DockerRegistry));
        Assert.NotNull(authConfig);
        Assert.Equal(DockerRegistry, authConfig.RegistryEndpoint);
        Assert.Equal(expectedUsername, authConfig.Username);
        Assert.Equal(expectedPassword, authConfig.Password);
        Assert.Equal(expectedIdentityToken, authConfig.IdentityToken);
      }
    }
  }
}
