﻿using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace SESCAP.Ecommerce;

public class RouteSlugifyParameterTransformer : IOutboundParameterTransformer
{
    public string TransformOutbound(object value)
    {
        if(value is null)
        {
            return null;
        }

        return Regex.Replace(
        value.ToString()!,
        "([a-z])([A-Z])",
        "$1-$2",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)).ToLowerInvariant(); 
    }
}
