using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TiaAgent.Cli.Release;

/// <summary>
/// Deterministic SPDX 2.3 JSON Software Bill of Materials (SBOM) generator.
/// </summary>
public static class SbomGenerator
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

    public static string GenerateSpdxJson(string productVersion, string commitSha, DateTimeOffset timestamp)
    {
        var docNamespace = $"https://github.com/industrix-com-br/tia-portal-code-agent/spdx/{productVersion}";
        var createdStr = timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var sbomData = new Dictionary<string, object>
        {
            ["spdxVersion"] = "SPDX-2.3",
            ["dataLicense"] = "CC0-1.0",
            ["SPDXID"] = "SPDXRef-DOCUMENT",
            ["name"] = $"TiaAgent-{productVersion}",
            ["documentNamespace"] = docNamespace,
            ["creationInfo"] = new Dictionary<string, object>
            {
                ["creators"] = new[]
                {
                    "Tool: TiaAgentReleaseTool-1.0",
                    "Organization: industrix-com-br"
                },
                ["created"] = createdStr
            },
            ["packages"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["name"] = "TiaAgent",
                    ["SPDXID"] = "SPDXRef-Package-TiaAgent",
                    ["versionInfo"] = productVersion,
                    ["downloadLocation"] = "https://github.com/industrix-com-br/tia-portal-code-agent/releases",
                    ["filesAnalyzed"] = false,
                    ["licenseConcluded"] = "MIT",
                    ["licenseDeclared"] = "MIT",
                    ["copyrightText"] = "Copyright (c) 2026 industrix-com-br",
                    ["supplier"] = "Organization: industrix-com-br",
                    ["comment"] = $"Built from commit {commitSha}"
                },
                new Dictionary<string, object>
                {
                    ["name"] = "TiaAgent.AddIn",
                    ["SPDXID"] = "SPDXRef-Package-TiaAgent.AddIn",
                    ["versionInfo"] = productVersion,
                    ["downloadLocation"] = "NOASSERTION",
                    ["filesAnalyzed"] = false,
                    ["licenseConcluded"] = "MIT",
                    ["licenseDeclared"] = "MIT",
                    ["copyrightText"] = "Copyright (c) 2026 industrix-com-br",
                    ["comment"] = "Siemens TIA Portal V21 Add-In component (.NET Framework 4.8)"
                },
                new Dictionary<string, object>
                {
                    ["name"] = "TiaAgent.Bridge",
                    ["SPDXID"] = "SPDXRef-Package-TiaAgent.Bridge",
                    ["versionInfo"] = productVersion,
                    ["downloadLocation"] = "NOASSERTION",
                    ["filesAnalyzed"] = false,
                    ["licenseConcluded"] = "MIT",
                    ["licenseDeclared"] = "MIT",
                    ["copyrightText"] = "Copyright (c) 2026 industrix-com-br",
                    ["comment"] = "TIA Agent HTTP Bridge component (.NET 8.0)"
                },
                new Dictionary<string, object>
                {
                    ["name"] = "TiaAgent.Cli",
                    ["SPDXID"] = "SPDXRef-Package-TiaAgent.Cli",
                    ["versionInfo"] = productVersion,
                    ["downloadLocation"] = "NOASSERTION",
                    ["filesAnalyzed"] = false,
                    ["licenseConcluded"] = "MIT",
                    ["licenseDeclared"] = "MIT",
                    ["copyrightText"] = "Copyright (c) 2026 industrix-com-br",
                    ["comment"] = "TIA Agent CLI Global Tool (.NET 8.0)"
                },
                new Dictionary<string, object>
                {
                    ["name"] = "TiaAgent.Contracts",
                    ["SPDXID"] = "SPDXRef-Package-TiaAgent.Contracts",
                    ["versionInfo"] = productVersion,
                    ["downloadLocation"] = "NOASSERTION",
                    ["filesAnalyzed"] = false,
                    ["licenseConcluded"] = "MIT",
                    ["licenseDeclared"] = "MIT",
                    ["copyrightText"] = "Copyright (c) 2026 industrix-com-br",
                    ["comment"] = "TIA Agent Shared DTO and Contract library (.NET Standard 2.0)"
                }
            },
            ["relationships"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["spdxElementId"] = "SPDXRef-DOCUMENT",
                    ["relationshipType"] = "DESCRIBES",
                    ["relatedSpdxElement"] = "SPDXRef-Package-TiaAgent"
                },
                new Dictionary<string, object>
                {
                    ["spdxElementId"] = "SPDXRef-Package-TiaAgent",
                    ["relationshipType"] = "CONTAINS",
                    ["relatedSpdxElement"] = "SPDXRef-Package-TiaAgent.AddIn"
                },
                new Dictionary<string, object>
                {
                    ["spdxElementId"] = "SPDXRef-Package-TiaAgent",
                    ["relationshipType"] = "CONTAINS",
                    ["relatedSpdxElement"] = "SPDXRef-Package-TiaAgent.Bridge"
                },
                new Dictionary<string, object>
                {
                    ["spdxElementId"] = "SPDXRef-Package-TiaAgent",
                    ["relationshipType"] = "CONTAINS",
                    ["relatedSpdxElement"] = "SPDXRef-Package-TiaAgent.Cli"
                },
                new Dictionary<string, object>
                {
                    ["spdxElementId"] = "SPDXRef-Package-TiaAgent",
                    ["relationshipType"] = "DEPENDS_ON",
                    ["relatedSpdxElement"] = "SPDXRef-Package-TiaAgent.Contracts"
                }
            }
        };

        return JsonSerializer.Serialize(sbomData, s_jsonOptions);
    }

    public static void WriteSbom(string directory, string productVersion, string commitSha, DateTimeOffset timestamp)
    {
        Directory.CreateDirectory(directory);
        var sbomPath = Path.Combine(directory, ReleaseStore.SbomFileName);
        var content = GenerateSpdxJson(productVersion, commitSha, timestamp);
        File.WriteAllText(sbomPath, content + "\n", Encoding.UTF8);
    }
}
