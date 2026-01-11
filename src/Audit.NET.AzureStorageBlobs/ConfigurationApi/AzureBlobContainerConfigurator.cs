using System;
using System.Collections.Generic;
using Audit.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Audit.AzureStorageBlobs.ConfigurationApi;

public class AzureBlobContainerConfigurator : IAzureBlobContainerConfigurator
{
    internal Setting<AccessTier?> _accessTier;
    internal Setting<string> _blobName;
    internal BlobClientOptions _clientOptions;
    internal Setting<string> _containerName;
    internal Setting<IDictionary<string, string>> _metadata;
    internal Setting<IDictionary<string, string>> _tags;

    public IAzureBlobContainerConfigurator BlobName(Func<AuditEvent, string> blobNameBuilder)
    {
        _blobName = blobNameBuilder;

        return this;
    }

    public IAzureBlobContainerConfigurator ClientOptions(BlobClientOptions options)
    {
        _clientOptions = options;

        return this;
    }

    public IAzureBlobContainerConfigurator ContainerName(string containerName)
    {
        _containerName = containerName;

        return this;
    }

    public IAzureBlobContainerConfigurator ContainerName(Func<AuditEvent, string> containerNameBuilder)
    {
        _containerName = containerNameBuilder;

        return this;
    }

    public IAzureBlobContainerConfigurator AccessTier(AccessTier accessTier)
    {
        _accessTier = accessTier;

        return this;
    }

    public IAzureBlobContainerConfigurator AccessTier(Func<AuditEvent, AccessTier?> accessTierBuilder)
    {
        _accessTier = accessTierBuilder;

        return this;
    }

    public IAzureBlobContainerConfigurator Metadata(Func<AuditEvent, IDictionary<string, string>> metadataBuilder)
    {
        _metadata = metadataBuilder;

        return this;
    }

    public IAzureBlobContainerConfigurator Tags(Func<AuditEvent, IDictionary<string, string>> tagsBuilder)
    {
        _tags = tagsBuilder;

        return this;
    }
}