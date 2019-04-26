﻿using Com.Ctrip.Framework.Apollo.Core.Utils;
using Com.Ctrip.Framework.Apollo.Enums;
using Com.Ctrip.Framework.Apollo.Logging;
using Com.Ctrip.Framework.Apollo.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Com.Ctrip.Framework.Apollo.Internals
{
    public class DefaultConfig : AbstractConfig, IRepositoryChangeListener, IDisposable
    {
        private static readonly ILogger Logger = LogManager.CreateLogger(typeof(DefaultConfig));
        private readonly string _namespace;
        private readonly ThreadSafe.AtomicReference<Properties> _configProperties = new ThreadSafe.AtomicReference<Properties>(null);
        private readonly IConfigRepository _configRepository;
        private readonly SemaphoreSlim _waitHandle = new SemaphoreSlim(1, 1);

        public DefaultConfig(string namespaceName, IConfigRepository configRepository)
        {
            _namespace = namespaceName;
            _configRepository = configRepository;
        }

        public async Task Initialize()
        {
            await _configRepository.Initialize().ConfigureAwait(false);

            try
            {
                _configProperties.WriteFullFence(_configRepository.GetConfig());
            }
            catch (Exception ex)
            {
                Logger.Warn($"Init Apollo Local Config failed - namespace: {_namespace}", ex);
            }
            finally
            {
                //register the change listener no matter config repository is working or not
                //so that whenever config repository is recovered, config could get changed
                _configRepository.AddChangeListener(this);
            }
        }

        public override string GetProperty(string key, string defaultValue)
        {
            // step 1: check system properties, i.e. -Dkey=value
            //TODO looks like .Net doesn't have such system property?
            string value = null;

            // step 2: check local cached properties file
            if (_configProperties.ReadFullFence() != null)
            {
                value = _configProperties.ReadFullFence().GetProperty(key);
            }

            // step 3: check env variable, i.e. PATH=...
            // normally system environment variables are in UPPERCASE, however there might be exceptions.
            // so the caller should provide the key in the right case
            if (value == null)
            {
                value = Environment.GetEnvironmentVariable(key);
            }

            //TODO step 4: check properties file from classpath


            if (value == null && _configProperties.ReadFullFence() == null)
            {
                Logger.Warn($"Could not load config for namespace {_namespace} from Apollo, please check whether the configs are released in Apollo! Return default value now!");
            }

            return value ?? defaultValue;
        }

        public void OnRepositoryChange(string namespaceName, Properties newProperties)
        {
            lock (this)
            {
                var newConfigProperties = new Properties(newProperties);

                var actualChanges = UpdateAndCalcConfigChanges(newConfigProperties);

                //check double checked result
                if (actualChanges.Count == 0)
                {
                    return;
                }

                FireConfigChange(new ConfigChangeEventArgs(_namespace, actualChanges));
            }
        }

        private IReadOnlyDictionary<string, ConfigChange> UpdateAndCalcConfigChanges(Properties newConfigProperties)
        {
            var configChanges = CalcPropertyChanges(_namespace, _configProperties.ReadFullFence(), newConfigProperties);

            var actualChanges = new Dictionary<string, ConfigChange>();

            //1. use getProperty to update configChanges's old value
            foreach (var change in configChanges)
            {
                change.OldValue = GetProperty(change.PropertyName, change.OldValue);
            }

            //2. update _configProperties
            _configProperties.WriteFullFence(newConfigProperties);

            //3. use getProperty to update configChange's new value and calc the final changes
            foreach (var change in configChanges)
            {
                change.NewValue = GetProperty(change.PropertyName, change.NewValue);
                switch (change.ChangeType)
                {
                    case PropertyChangeType.Added:
                        if (string.Equals(change.OldValue, change.NewValue))
                        {
                            break;
                        }
                        if (change.OldValue != null)
                        {
                            change.ChangeType = PropertyChangeType.Modified;
                        }
                        actualChanges[change.PropertyName] = change;
                        break;
                    case PropertyChangeType.Modified:
                        if (!string.Equals(change.OldValue, change.NewValue))
                        {
                            actualChanges[change.PropertyName] = change;
                        }
                        break;
                    case PropertyChangeType.Deleted:
                        if (string.Equals(change.OldValue, change.NewValue))
                        {
                            break;
                        }
                        if (change.NewValue != null)
                        {
                            change.ChangeType = PropertyChangeType.Modified;
                        }
                        actualChanges[change.PropertyName] = change;
                        break;
                }
            }
            return actualChanges;
        }

        public override ISet<string> GetPropertyNames()
        {
            var properties = _configProperties.ReadFullFence();
            return properties == null ? new HashSet<string>() : properties.GetPropertyNames();
        }

        public void Dispose()
        {
            _waitHandle?.Dispose();
        }
    }
}

