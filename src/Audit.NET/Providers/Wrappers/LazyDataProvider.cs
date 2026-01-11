using System;

namespace Audit.Core.Providers.Wrappers;

/// <summary>
/// A lazy data provider that allows to lazily instantiate the data provider to use. The data provider factory method will
/// be called only once; the first time it's needed.
/// </summary>
public class LazyDataProvider : WrapperDataProvider
{
    private Lazy<IAuditDataProvider> _factory;

    public LazyDataProvider() { }

    public LazyDataProvider(Func<IAuditDataProvider> dataProviderFactory)
    {
        _factory = new Lazy<IAuditDataProvider>(dataProviderFactory);
    }

    public Func<IAuditDataProvider> Factory
    {
        set => _factory = new Lazy<IAuditDataProvider>(value);
    }

    protected override IAuditDataProvider GetDataProvider(AuditEvent auditEvent)
    {
        return _factory.Value;
    }
}