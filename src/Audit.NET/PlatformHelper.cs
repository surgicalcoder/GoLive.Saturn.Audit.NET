using System;

namespace Audit.Core;

internal static class PlatformHelper
{
    private static readonly Lazy<bool> IsRunningOnMonoValue = new(() => { return Type.GetType("Mono.Runtime") != null; });

    public static bool IsRunningOnMono()
    {
        return IsRunningOnMonoValue.Value;
    }
}