#if UNITY_EDITOR
using System;
using RabbitMQ.Client;
using UnityEditor;
using UnityEngine;

internal static class RabbitMqPluginConfigurator
{
    private static readonly string[] PluginPaths =
    {
        "Assets/Plugins/RabbitMQ/RabbitMQ.Client.dll",
        "Assets/Plugins/RabbitMQ/System.Buffers.dll",
        "Assets/Plugins/RabbitMQ/System.Memory.dll",
        "Assets/Plugins/RabbitMQ/System.Runtime.CompilerServices.Unsafe.dll",
        "Assets/Plugins/RabbitMQ/System.Threading.Channels.dll",
        "Assets/Plugins/RabbitMQ/System.Threading.Tasks.Extensions.dll",
        "Assets/Plugins/RabbitMQ/Microsoft.Bcl.AsyncInterfaces.dll"
    };

    private static readonly Type EnsureType = typeof(IConnectionFactory);

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        Debug.Log("RabbitMqPluginConfigurator executing");
        ConfigurePlugins();
    }

    public static void ConfigurePlugins()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        foreach (var path in PluginPaths)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as PluginImporter;
            if (importer == null)
            {
                Debug.LogWarning($"RabbitMqPluginConfigurator: importer not found for {path}");
                continue;
            }

            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(true);
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows, true);
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
            importer.SaveAndReimport();
        }
    }
}
#endif
