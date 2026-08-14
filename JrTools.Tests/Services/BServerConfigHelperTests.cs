using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using JrTools.Dto;
using JrTools.Services.Db;
using Xunit;

namespace JrTools.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="BServerConfigHelper"/>.
    /// Validates: Requirements 3.1, 3.2, 3.3, 3.6
    /// </summary>
    public class BServerConfigHelperTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _originalLocalAppData;

        public BServerConfigHelperTests()
        {
            // Create a temporary test directory for configuration files
            _testDirectory = Path.Combine(Path.GetTempPath(), "JrToolsTests_BServerConfig_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);

            // Override LocalApplicationData environment variable for testing
            _originalLocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Environment.SetEnvironmentVariable("LOCALAPPDATA", _testDirectory, EnvironmentVariableTarget.Process);
        }

        public void Dispose()
        {
            // Restore original LocalApplicationData
            Environment.SetEnvironmentVariable("LOCALAPPDATA", _originalLocalAppData, EnvironmentVariableTarget.Process);

            // Clean up test directory
            try
            {
                if (Directory.Exists(_testDirectory))
                    Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Fact]
        public async Task LerAsync_WhenFileDoesNotExist_ReturnsDefaultBServerConfigDto()
        {
            // Arrange - ensure no config file exists
            var configPath = Path.Combine(_testDirectory, "JrTools", "bserver-config.json");
            if (File.Exists(configPath))
                File.Delete(configPath);

            // Act
            var result = await BServerConfigHelper.LerAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.ServerAddress);
            Assert.Equal(2000, result.Port);
            Assert.Equal(30, result.TimeoutSeconds);
            Assert.Empty(result.RecentServers);
            Assert.Null(result.LastConnectionAttempt);
            Assert.Empty(result.CachedSystems);
            Assert.Null(result.SystemsCacheExpiry);
        }

        [Fact]
        public async Task LerAsync_WhenFileIsCorrupted_ReturnsDefaultBServerConfigDto()
        {
            // Arrange - create a corrupted JSON file
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            Directory.CreateDirectory(jrToolsDir);
            var configPath = Path.Combine(jrToolsDir, "bserver-config.json");
            
            // Write invalid JSON
            await File.WriteAllTextAsync(configPath, "{ invalid json content }");

            // Act
            var result = await BServerConfigHelper.LerAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.ServerAddress);
            Assert.Equal(2000, result.Port);
            Assert.Equal(30, result.TimeoutSeconds);
            Assert.Empty(result.RecentServers);
            Assert.Null(result.LastConnectionAttempt);
            Assert.Empty(result.CachedSystems);
            Assert.Null(result.SystemsCacheExpiry);
        }

        [Fact]
        public async Task LerAsync_WhenFileContainsInvalidJson_ReturnsDefaultBServerConfigDto()
        {
            // Arrange - create a file with completely invalid content
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            Directory.CreateDirectory(jrToolsDir);
            var configPath = Path.Combine(jrToolsDir, "bserver-config.json");
            
            // Write non-JSON content
            await File.WriteAllTextAsync(configPath, "This is not JSON at all!");

            // Act
            var result = await BServerConfigHelper.LerAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.ServerAddress);
            Assert.Equal(2000, result.Port);
            Assert.Equal(30, result.TimeoutSeconds);
            Assert.Empty(result.RecentServers);
            Assert.Null(result.LastConnectionAttempt);
            Assert.Empty(result.CachedSystems);
            Assert.Null(result.SystemsCacheExpiry);
        }

        [Fact]
        public async Task LerAsync_WhenFileIsEmpty_ReturnsDefaultBServerConfigDto()
        {
            // Arrange - create an empty file
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            Directory.CreateDirectory(jrToolsDir);
            var configPath = Path.Combine(jrToolsDir, "bserver-config.json");
            
            // Write empty content
            await File.WriteAllTextAsync(configPath, "");

            // Act
            var result = await BServerConfigHelper.LerAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(string.Empty, result.ServerAddress);
            Assert.Equal(2000, result.Port);
            Assert.Equal(30, result.TimeoutSeconds);
        }

        [Fact]
        public async Task SalvarAsync_WhenDirectoryDoesNotExist_CreatesDirectoryStructure()
        {
            // Arrange
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            
            // Ensure directory doesn't exist
            if (Directory.Exists(jrToolsDir))
                Directory.Delete(jrToolsDir, recursive: true);

            var config = new BServerConfigDto
            {
                ServerAddress = "127.0.0.1",
                Port = 2001,
                TimeoutSeconds = 60
            };

            // Act
            await BServerConfigHelper.SalvarAsync(config);

            // Assert
            Assert.True(Directory.Exists(jrToolsDir));
            
            var configPath = Path.Combine(jrToolsDir, "bserver-config.json");
            Assert.True(File.Exists(configPath));
        }

        [Fact]
        public async Task SalvarAsync_PersistsBServerConfigDtoToJsonFileCorrectly()
        {
            // Arrange
            var config = new BServerConfigDto
            {
                ServerAddress = "192.168.1.100",
                Port = 2010,
                TimeoutSeconds = 45,
                RecentServers = { "server1.local", "server2.local" },
                LastConnectionAttempt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                CachedSystems = { "SISTEMA_RH", "SISTEMA_FINANCEIRO" },
                SystemsCacheExpiry = new DateTime(2024, 1, 15, 10, 35, 0, DateTimeKind.Utc)
            };

            // Act
            await BServerConfigHelper.SalvarAsync(config);

            // Assert - verify file was created with correct content
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            var configPath = Path.Combine(jrToolsDir, "bserver-config.json");
            
            Assert.True(File.Exists(configPath));

            var savedJson = await File.ReadAllTextAsync(configPath);
            var savedConfig = JsonSerializer.Deserialize<BServerConfigDto>(savedJson);

            Assert.NotNull(savedConfig);
            Assert.Equal("192.168.1.100", savedConfig.ServerAddress);
            Assert.Equal(2010, savedConfig.Port);
            Assert.Equal(45, savedConfig.TimeoutSeconds);
            Assert.Equal(2, savedConfig.RecentServers.Count);
            Assert.Contains("server1.local", savedConfig.RecentServers);
            Assert.Contains("server2.local", savedConfig.RecentServers);
            Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc), savedConfig.LastConnectionAttempt);
            Assert.Equal(2, savedConfig.CachedSystems.Count);
            Assert.Contains("SISTEMA_RH", savedConfig.CachedSystems);
            Assert.Contains("SISTEMA_FINANCEIRO", savedConfig.CachedSystems);
            Assert.Equal(new DateTime(2024, 1, 15, 10, 35, 0, DateTimeKind.Utc), savedConfig.SystemsCacheExpiry);
        }

        [Fact]
        public async Task SalvarAsync_GeneratesFormattedJsonWithIndentation()
        {
            // Arrange
            var config = new BServerConfigDto
            {
                ServerAddress = "test.server.com",
                Port = 2000,
                TimeoutSeconds = 30
            };

            // Act
            await BServerConfigHelper.SalvarAsync(config);

            // Assert - verify JSON is formatted (has indentation)
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            var configPath = Path.Combine(jrToolsDir, "bserver-config.json");
            var savedJson = await File.ReadAllTextAsync(configPath);

            // Check that JSON contains newlines and spaces (indicating WriteIndented = true)
            Assert.Contains("\n", savedJson);
            Assert.Contains("  ", savedJson); // Indentation spaces
            
            // Verify it's still valid JSON by deserializing
            var deserializedConfig = JsonSerializer.Deserialize<BServerConfigDto>(savedJson);
            Assert.NotNull(deserializedConfig);
            Assert.Equal("test.server.com", deserializedConfig.ServerAddress);
        }

        [Fact]
        public async Task RoundTripTest_SaveConfigThenLoadItBack_VerifiesPersistence()
        {
            // Arrange - create a comprehensive configuration
            var originalConfig = new BServerConfigDto
            {
                ServerAddress = "production.bserver.com",
                Port = 2500,
                TimeoutSeconds = 120,
                RecentServers = { "dev.bserver.com", "test.bserver.com", "production.bserver.com" },
                LastConnectionAttempt = new DateTime(2024, 2, 20, 14, 45, 30, DateTimeKind.Utc),
                CachedSystems = { "SISTEMA_RH", "SISTEMA_CONTABIL", "SISTEMA_CRM", "SISTEMA_ESTOQUE" },
                SystemsCacheExpiry = new DateTime(2024, 2, 20, 14, 50, 30, DateTimeKind.Utc)
            };

            // Act - save and then load the configuration
            await BServerConfigHelper.SalvarAsync(originalConfig);
            var loadedConfig = await BServerConfigHelper.LerAsync();

            // Assert - verify all data was persisted and loaded correctly
            Assert.NotNull(loadedConfig);
            Assert.Equal(originalConfig.ServerAddress, loadedConfig.ServerAddress);
            Assert.Equal(originalConfig.Port, loadedConfig.Port);
            Assert.Equal(originalConfig.TimeoutSeconds, loadedConfig.TimeoutSeconds);
            
            // Verify RecentServers
            Assert.Equal(originalConfig.RecentServers.Count, loadedConfig.RecentServers.Count);
            foreach (var server in originalConfig.RecentServers)
            {
                Assert.Contains(server, loadedConfig.RecentServers);
            }
            
            Assert.Equal(originalConfig.LastConnectionAttempt, loadedConfig.LastConnectionAttempt);
            
            // Verify CachedSystems
            Assert.Equal(originalConfig.CachedSystems.Count, loadedConfig.CachedSystems.Count);
            foreach (var system in originalConfig.CachedSystems)
            {
                Assert.Contains(system, loadedConfig.CachedSystems);
            }
            
            Assert.Equal(originalConfig.SystemsCacheExpiry, loadedConfig.SystemsCacheExpiry);
        }

        [Fact]
        public async Task LerAsync_WhenValidJsonExists_LoadsConfigurationCorrectly()
        {
            // Arrange - create a valid JSON configuration file manually
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            Directory.CreateDirectory(jrToolsDir);
            var configPath = Path.Combine(jrToolsDir, "bserver-config.json");
            
            var testConfig = new BServerConfigDto
            {
                ServerAddress = "manual.test.server",
                Port = 3000,
                TimeoutSeconds = 90,
                RecentServers = { "recent1", "recent2" },
                LastConnectionAttempt = new DateTime(2024, 3, 1, 9, 0, 0, DateTimeKind.Utc),
                CachedSystems = { "SYSTEM1", "SYSTEM2" },
                SystemsCacheExpiry = new DateTime(2024, 3, 1, 9, 5, 0, DateTimeKind.Utc)
            };

            var json = JsonSerializer.Serialize(testConfig, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configPath, json);

            // Act
            var result = await BServerConfigHelper.LerAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("manual.test.server", result.ServerAddress);
            Assert.Equal(3000, result.Port);
            Assert.Equal(90, result.TimeoutSeconds);
            Assert.Equal(2, result.RecentServers.Count);
            Assert.Contains("recent1", result.RecentServers);
            Assert.Contains("recent2", result.RecentServers);
            Assert.Equal(new DateTime(2024, 3, 1, 9, 0, 0, DateTimeKind.Utc), result.LastConnectionAttempt);
            Assert.Equal(2, result.CachedSystems.Count);
            Assert.Contains("SYSTEM1", result.CachedSystems);
            Assert.Contains("SYSTEM2", result.CachedSystems);
            Assert.Equal(new DateTime(2024, 3, 1, 9, 5, 0, DateTimeKind.Utc), result.SystemsCacheExpiry);
        }

        [Fact]
        public async Task SalvarAsync_WhenFileAlreadyExists_OverwritesExistingFile()
        {
            // Arrange - create initial configuration
            var initialConfig = new BServerConfigDto
            {
                ServerAddress = "initial.server",
                Port = 1000,
                TimeoutSeconds = 15
            };
            
            await BServerConfigHelper.SalvarAsync(initialConfig);
            
            // Verify initial file was created
            var loadedInitial = await BServerConfigHelper.LerAsync();
            Assert.Equal("initial.server", loadedInitial.ServerAddress);
            Assert.Equal(1000, loadedInitial.Port);

            // Act - save new configuration
            var updatedConfig = new BServerConfigDto
            {
                ServerAddress = "updated.server",
                Port = 2000,
                TimeoutSeconds = 60,
                RecentServers = { "new.server" }
            };
            
            await BServerConfigHelper.SalvarAsync(updatedConfig);

            // Assert - verify file was overwritten
            var loadedUpdated = await BServerConfigHelper.LerAsync();
            Assert.Equal("updated.server", loadedUpdated.ServerAddress);
            Assert.Equal(2000, loadedUpdated.Port);
            Assert.Equal(60, loadedUpdated.TimeoutSeconds);
            Assert.Single(loadedUpdated.RecentServers);
            Assert.Contains("new.server", loadedUpdated.RecentServers);
        }

        [Fact]
        public async Task LerAsync_WhenJsonContainsNullValues_HandlesNullsGracefully()
        {
            // Arrange - create JSON with explicit nulls for nullable properties
            var jrToolsDir = Path.Combine(_testDirectory, "JrTools");
            Directory.CreateDirectory(jrToolsDir);
            var configPath = Path.Combine(jrToolsDir, "bserver-config.json");
            
            var jsonWithNulls = @"{
                ""ServerAddress"": ""test.server"",
                ""Port"": 2000,
                ""TimeoutSeconds"": 30,
                ""RecentServers"": [],
                ""LastConnectionAttempt"": null,
                ""CachedSystems"": [],
                ""SystemsCacheExpiry"": null
            }";
            
            await File.WriteAllTextAsync(configPath, jsonWithNulls);

            // Act
            var result = await BServerConfigHelper.LerAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test.server", result.ServerAddress);
            Assert.Equal(2000, result.Port);
            Assert.Equal(30, result.TimeoutSeconds);
            Assert.Empty(result.RecentServers);
            Assert.Null(result.LastConnectionAttempt);
            Assert.Empty(result.CachedSystems);
            Assert.Null(result.SystemsCacheExpiry);
        }
    }
}