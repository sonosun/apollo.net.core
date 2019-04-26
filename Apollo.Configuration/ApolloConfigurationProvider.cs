﻿using Com.Ctrip.Framework.Apollo.Core.Utils;
using Com.Ctrip.Framework.Apollo.Internals;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Com.Ctrip.Framework.Apollo
{
    public class ApolloConfigurationProvider : ConfigurationProvider, IRepositoryChangeListener, IConfigurationSource
    {
        private readonly string _sectionKey;
        private readonly IConfigRepository _configRepository;
        private readonly Task _initializeTask;

        public ApolloConfigurationProvider(string sectionKey, IConfigRepository configRepository)
        {
            _sectionKey = sectionKey;
            _configRepository = configRepository;
            _initializeTask = _configRepository.Initialize();
        }

        public override void Load()
        {
            _initializeTask.ConfigureAwait(false).GetAwaiter().GetResult();

            _configRepository.AddChangeListener(this);

            SetData(_configRepository.GetConfig());
        }

        private void SetData(Properties properties)
        {
            if (string.IsNullOrEmpty(_sectionKey) || properties.Source == null || properties.Source.Count == 0)
                Data = properties.Source;
            else
            {
                var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kv in properties.Source)
                    data[$"{_sectionKey}{ConfigurationPath.KeyDelimiter}{kv.Key}"] = kv.Value;

                Data = data;
            }
        }

        void IRepositoryChangeListener.OnRepositoryChange(string namespaceName, Properties newProperties)
        {
            SetData(newProperties);

            OnReload();
        }

        IConfigurationProvider IConfigurationSource.Build(IConfigurationBuilder builder) => this;
    }
}
