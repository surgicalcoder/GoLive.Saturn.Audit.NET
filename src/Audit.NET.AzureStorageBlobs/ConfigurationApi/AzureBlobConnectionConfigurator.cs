using System;

namespace Audit.AzureStorageBlobs.ConfigurationApi;

public class AzureBlobConnectionConfigurator : IAzureBlobConnectionConfigurator
{
    internal string _connectionString;
    internal AzureBlobContainerConfigurator _containerConfig = new();
    internal AzureBlobCredentialConfiguration _credentialConfig;

    public IAzureBlobContainerConfigurator WithConnectionString(string connectionString)
    {
        _connectionString = connectionString;

        return _containerConfig;
    }

    public IAzureBlobContainerConfigurator WithCredentials(Action<IAzureBlobCredentialConfiguration> credentialConfig)
    {
        _credentialConfig = new AzureBlobCredentialConfiguration();
        credentialConfig.Invoke(_credentialConfig);

        return _containerConfig;
    }

    public IAzureBlobContainerConfigurator WithServiceUrl(string serviceUrl)
    {
        _credentialConfig = new AzureBlobCredentialConfiguration();
        _credentialConfig.Url(serviceUrl);

        return _containerConfig;
    }
}