using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace AdaptationCore
{
    public static class ConfigLoader
    {
        public static ConfigPackage LoadByVersion(string baseDirectory, string version)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            }

            var versionDirectory = Path.Combine(baseDirectory, version);
            return LoadFromDirectory(versionDirectory);
        }

        public static ConfigPackage LoadFromDirectory(string configDirectory)
        {
            if (string.IsNullOrWhiteSpace(configDirectory))
            {
                throw new ArgumentException("Config directory is required.", nameof(configDirectory));
            }

            var path = Path.Combine(configDirectory, "config.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Config file not found.", path);
            }

            var json = File.ReadAllText(path, Encoding.UTF8);
            ConfigPackage config;
            using (var stream = File.OpenRead(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(ConfigPackage));
                config = (ConfigPackage)serializer.ReadObject(stream);
            }
            config ??= new ConfigPackage();
            config.version_hash = ComputeHash(json);
            return config;
        }

        private static string ComputeHash(string content)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }
}
