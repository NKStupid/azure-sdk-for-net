// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Azure.Core;

namespace Azure.Search.Documents.Indexes.Models
{
    public partial class CommonGramTokenFilter : IUtf8JsonSerializable
    {
        void IUtf8JsonSerializable.Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            if (CommonWords != null && CommonWords.Any())
            {
                writer.WritePropertyName("commonWords");
                writer.WriteStartArray();
                foreach (var item in CommonWords)
                {
                    writer.WriteStringValue(item);
                }
                writer.WriteEndArray();
            }
            if (IgnoreCase != null)
            {
                writer.WritePropertyName("ignoreCase");
                writer.WriteBooleanValue(IgnoreCase.Value);
            }
            if (UseQueryMode != null)
            {
                writer.WritePropertyName("queryMode");
                writer.WriteBooleanValue(UseQueryMode.Value);
            }
            writer.WritePropertyName("@odata.type");
            writer.WriteStringValue(ODataType);
            writer.WritePropertyName("name");
            writer.WriteStringValue(Name);
            writer.WriteEndObject();
        }

        internal static CommonGramTokenFilter DeserializeCommonGramTokenFilter(JsonElement element)
        {
            IList<string> commonWords = default;
            bool? ignoreCase = default;
            bool? queryMode = default;
            string odataType = default;
            string name = default;
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("commonWords"))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    List<string> array = new List<string>();
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Null)
                        {
                            array.Add(null);
                        }
                        else
                        {
                            array.Add(item.GetString());
                        }
                    }
                    commonWords = array;
                    continue;
                }
                if (property.NameEquals("ignoreCase"))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    ignoreCase = property.Value.GetBoolean();
                    continue;
                }
                if (property.NameEquals("queryMode"))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    queryMode = property.Value.GetBoolean();
                    continue;
                }
                if (property.NameEquals("@odata.type"))
                {
                    odataType = property.Value.GetString();
                    continue;
                }
                if (property.NameEquals("name"))
                {
                    name = property.Value.GetString();
                    continue;
                }
            }
            return new CommonGramTokenFilter(odataType, name, commonWords, ignoreCase, queryMode);
        }
    }
}
