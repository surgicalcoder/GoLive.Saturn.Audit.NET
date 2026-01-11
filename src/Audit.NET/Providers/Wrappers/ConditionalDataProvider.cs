using System;
using System.Collections.Generic;
using Audit.Core.ConfigurationApi;

namespace Audit.Core.Providers.Wrappers;

/// <summary>
/// This provider enables the configuration of different data providers based on conditions related to the audit event.
/// </summary>
public class ConditionalDataProvider : WrapperDataProvider
{
    public ConditionalDataProvider() { }

    public ConditionalDataProvider(Action<IConditionalDataProviderConfigurator> config)
    {
        var conditionalConfig = new ConditionalDataProviderConfigurator();

        if (config != null)
        {
            config.Invoke(conditionalConfig);
            GuardConditions = conditionalConfig._guardConditions;
        }
    }

    /// <summary>
    /// The list of guarded data providers.
    /// </summary>
    public List<GuardCondition> GuardConditions { get; set; } = [];

    /// <summary>
    /// Returns the data provider for a given audit event, or NULL if no condition is met.
    /// </summary>
    protected override IAuditDataProvider GetDataProvider(AuditEvent auditEvent)
    {
        if (GuardConditions != null)
        {
            foreach (var config in GuardConditions)
            {
                if (config.Guard(auditEvent))
                {
                    return config.DataProvider;
                }
            }
        }

        return null;
    }

    public class GuardCondition
    {
        public Func<AuditEvent, bool> Guard { get; set; }
        public IAuditDataProvider DataProvider { get; set; }
    }
}