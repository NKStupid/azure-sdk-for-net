// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using Azure.Core;

namespace Azure.Management.Resources.Models
{
    public partial class PolicyDefinitionReference : IUtf8JsonSerializable
    {
        void IUtf8JsonSerializable.Write(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("policyDefinitionId");
            writer.WriteStringValue(PolicyDefinitionId);
            if (Parameters != null)
            {
                writer.WritePropertyName("parameters");
                writer.WriteStartObject();
                foreach (var item in Parameters)
                {
                    writer.WritePropertyName(item.Key);
                    writer.WriteObjectValue(item.Value);
                }
                writer.WriteEndObject();
            }
            if (PolicyDefinitionReferenceId != null)
            {
                writer.WritePropertyName("policyDefinitionReferenceId");
                writer.WriteStringValue(PolicyDefinitionReferenceId);
            }
            if (GroupNames != null)
            {
                writer.WritePropertyName("groupNames");
                writer.WriteStartArray();
                foreach (var item in GroupNames)
                {
                    writer.WriteStringValue(item);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }

        internal static PolicyDefinitionReference DeserializePolicyDefinitionReference(JsonElement element)
        {
            string policyDefinitionId = default;
            IDictionary<string, ParameterValuesValue> parameters = default;
            string policyDefinitionReferenceId = default;
            IList<string> groupNames = default;
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("policyDefinitionId"))
                {
                    policyDefinitionId = property.Value.GetString();
                    continue;
                }
                if (property.NameEquals("parameters"))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    Dictionary<string, ParameterValuesValue> dictionary = new Dictionary<string, ParameterValuesValue>();
                    foreach (var property0 in property.Value.EnumerateObject())
                    {
                        if (property0.Value.ValueKind == JsonValueKind.Null)
                        {
                            dictionary.Add(property0.Name, null);
                        }
                        else
                        {
                            dictionary.Add(property0.Name, ParameterValuesValue.DeserializeParameterValuesValue(property0.Value));
                        }
                    }
                    parameters = dictionary;
                    continue;
                }
                if (property.NameEquals("policyDefinitionReferenceId"))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        continue;
                    }
                    policyDefinitionReferenceId = property.Value.GetString();
                    continue;
                }
                if (property.NameEquals("groupNames"))
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
                    groupNames = array;
                    continue;
                }
            }
            return new PolicyDefinitionReference(policyDefinitionId, parameters, policyDefinitionReferenceId, groupNames);
        }
    }
}
