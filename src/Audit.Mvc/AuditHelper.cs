using System.Collections.Generic;
using System.Linq;
using Audit.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;

namespace Audit.Mvc;

internal static class AuditHelper
{
    public static IDictionary<string, object> SerializeParameters(IDictionary<string, object> parameters)
    {
        return parameters?.ToDictionary(
            k => k.Key, 
            v => SerializeParameter(v.Value));
    }

    private static object SerializeParameter(object value)
    {
        if (value == null)
            return null;

        // Handle IFormFile specially - don't try to serialize it
        if (value is IFormFile formFile)
        {
            return new
            {
                FileName = formFile.FileName,
                ContentType = formFile.ContentType,
                Length = formFile.Length
            };
        }

        // Handle collections of IFormFile
        if (value is IEnumerable<IFormFile> formFiles)
        {
            return formFiles.Select(f => new
            {
                FileName = f.FileName,
                ContentType = f.ContentType,
                Length = f.Length
            }).ToList();
        }

        // Try to serialize and deserialize other types
        try
        {
            return Configuration.JsonAdapter.Deserialize(Configuration.JsonAdapter.Serialize(value), value.GetType());
        }
        catch
        {
            // If serialization fails, return a string representation
            return value.ToString();
        }
    }

    internal static Dictionary<string, string> GetModelStateErrors(ModelStateDictionary modelState)
    {
        if (modelState == null)
        {
            return null;
        }

        var dict = new Dictionary<string, string>();

        foreach (var state in modelState)
        {
            if (state.Value.Errors.Count > 0)
            {
                dict.Add(state.Key, string.Join(", ", state.Value.Errors.Select(e => e.ErrorMessage)));
            }
        }

        return dict.Count > 0 ? dict : null;
    }

    public static IDictionary<string, string> ToDictionary(IEnumerable<KeyValuePair<string, StringValues>> col)
    {
        if (col == null)
        {
            return null;
        }

        IDictionary<string, string> dict = new Dictionary<string, string>();

        foreach (var k in col)
        {
            dict.Add(k.Key, string.Join(", ", k.Value));
        }

        return dict;
    }
}