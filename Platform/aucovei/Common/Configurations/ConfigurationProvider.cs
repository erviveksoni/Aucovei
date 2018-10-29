using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.Common.Configurations
{
    public class ConfigurationProvider : IConfigurationProvider, IDisposable
    {
        readonly Dictionary<string, string> configuration = new Dictionary<string, string>();
        EnvironmentDescription environment = null;
        const string ConfigToken = "config:";
        bool _disposed = false;

        public string GetConfigurationSettingValue(string configurationSettingName)
        {
            return this.GetConfigurationSettingValueOrDefault(configurationSettingName, string.Empty);
        }

        public string GetConfigurationSettingValueOrDefault(string configurationSettingName, string defaultValue)
        {
            if (!this.configuration.ContainsKey(configurationSettingName))
            {
                string configValue = CloudConfigurationManager.GetSetting(configurationSettingName);
                bool isEmulated = Environment.CommandLine.Contains("iisexpress.exe") ||
                    Environment.CommandLine.Contains("w3wp.exe") ||
                    Process.GetCurrentProcess().GetAncestorNames().Contains("devenv"); // if debugging in VS, devenv will be the parent

                if (isEmulated && (configValue != null && configValue.StartsWith(ConfigToken, StringComparison.OrdinalIgnoreCase)))
                {
                    if (this.environment == null)
                    {
                        this.LoadEnvironmentConfig();
                    }

                    configValue = this.environment.GetSetting(
                        configValue.Substring(configValue.IndexOf(ConfigToken, StringComparison.Ordinal) + ConfigToken.Length));
                }

                try
                {
                    this.configuration.Add(configurationSettingName, configValue);
                }
                catch (ArgumentException)
                {
                    // at this point, this key has already been added on a different
                    // thread, so we're fine to continue
                }
            }

            return this.configuration[configurationSettingName] ?? defaultValue;
        }

        void LoadEnvironmentConfig()
        {
            var executingPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);

            // Check for build_output
            int buildLocation = executingPath.IndexOf("Build_Output", StringComparison.OrdinalIgnoreCase);
            if (buildLocation >= 0)
            {
                string fileName = executingPath.Substring(0, buildLocation) + "local.config.user";
                if (File.Exists(fileName))
                {
                    this.environment = new EnvironmentDescription(fileName);
                    return;
                }
            }

            // Web roles run in there app dir so look relative
            int location = executingPath.IndexOf("Web\\bin", StringComparison.OrdinalIgnoreCase);

            if (location == -1)
            {
                location = executingPath.IndexOf("WebJob\\bin", StringComparison.OrdinalIgnoreCase);
            }
            if (location >= 0)
            {
                string fileName = executingPath.Substring(0, location) + "..\\local.config.user";
                if (File.Exists(fileName))
                {
                    this.environment = new EnvironmentDescription(fileName);
                    return;
                }
            }

            throw new ArgumentException("Unable to locate local.config.user file.  Make sure you have run 'build.cmd local'.");
        }



        #region IDispose 
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this._disposed)
            {
                return;
            }

            if (disposing)
            {
                if (this.environment != null)
                {
                    this.environment.Dispose();
                }
            }

            this._disposed = true;
        }

        ~ConfigurationProvider()
        {
            this.Dispose(false);
        }
        #endregion
    }

    public static class ProcessExtensions
    {
        static private string _processCategory = "Process";
        static private string _pidCounter = "ID Process";
        static private string _parentCounter = "Creating Process ID";

        public static IEnumerable<string> GetAncestorNames(this Process process)
        {
            while (true)
            {
                process = process.GetParent();
                if (process == null)
                {
                    break;
                }

                yield return process.ProcessName;
            }
        }

        public static Process GetParent(this Process process)
        {
            for (int idx = 0; ; idx++)
            {
                var name = process.ProcessName;
                if (idx > 0)
                {
                    // name += FormattableString.Invariant($"#{idx}");
                    name += ($"#{idx}");
                }

                try
                {

                    using (var pidReader = new PerformanceCounter(_processCategory, _pidCounter, name))
                    {
                        if ((int)pidReader.NextValue() != process.Id)
                        {
                            continue;
                        }

                        using (var parentReader = new PerformanceCounter(_processCategory, _parentCounter, name))
                        {
                            return Process.GetProcessById((int)parentReader.NextValue());
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Trace.TraceError(FormattableString.Invariant($"Exception raised in GetParentname: {ex}"));
                    return null;
                }
            }
        }
    }
}
