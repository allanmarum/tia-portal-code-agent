using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace TiaAgent.ArchitectureTests;

public sealed class RepositoryHealthAndSecurityTests
{
    [Fact]
    public void Security_md_exists_and_references_security_model_spec()
    {
        var root = FindRepositoryRoot();
        var securityMdPath = Path.Combine(root, "SECURITY.md");

        File.Exists(securityMdPath).Should().BeTrue("SECURITY.md must exist in repository root");

        var content = File.ReadAllText(securityMdPath);
        content.Should().Contain("docs/spec/SECURITY_MODEL.md",
            "SECURITY.md must reference the authoritative Security & Safety Model specification");
        content.Should().Contain("Reporting a Vulnerability",
            "SECURITY.md must define vulnerability disclosure procedures");
    }

    [Fact]
    public void Security_model_spec_exists()
    {
        var root = FindRepositoryRoot();
        var specPath = Path.Combine(root, "docs", "spec", "SECURITY_MODEL.md");

        File.Exists(specPath).Should().BeTrue("docs/spec/SECURITY_MODEL.md must exist");
    }

    [Fact]
    public void Security_workflow_exists()
    {
        var root = FindRepositoryRoot();
        var workflowPath = Path.Combine(root, ".github", "workflows", "security.yml");

        File.Exists(workflowPath).Should().BeTrue(".github/workflows/security.yml must exist for automated security scanning");

        var content = File.ReadAllText(workflowPath);
        content.Should().Contain("CodeQL", "Security workflow must configure CodeQL static analysis");
    }

    [Fact]
    public void Codeowners_covers_security_policy_and_workflows()
    {
        var root = FindRepositoryRoot();
        var codeownersPath = Path.Combine(root, ".github", "CODEOWNERS");

        File.Exists(codeownersPath).Should().BeTrue(".github/CODEOWNERS must exist");

        var content = File.ReadAllText(codeownersPath);
        content.Should().Contain("/SECURITY.md", "CODEOWNERS must assign ownership of SECURITY.md");
        content.Should().Contain("/.github/", "CODEOWNERS must assign ownership of GitHub workflows");
    }

    [Fact]
    public void Issue_template_config_includes_security_reporting_link()
    {
        var root = FindRepositoryRoot();
        var configPath = Path.Combine(root, ".github", "ISSUE_TEMPLATE", "config.yml");

        File.Exists(configPath).Should().BeTrue(".github/ISSUE_TEMPLATE/config.yml must exist");

        var content = File.ReadAllText(configPath);
        content.Should().Contain("Security Vulnerability Report",
            "ISSUE_TEMPLATE config.yml must provide a link for reporting security vulnerabilities");
    }

    [Fact]
    public void AddIn_Config_xml_maintains_least_privilege_read_only()
    {
        var root = FindRepositoryRoot();
        var configPath = Path.Combine(root, "src", "TiaAgent.AddIn", "Config.xml");

        var document = XDocument.Load(configPath);
        var permissions = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "TIAPermissions")?.Elements().Select(e => e.Name.LocalName).ToList() ?? new List<string>();

        permissions.Should().Contain("TIA.ReadOnly", "MVP Add-In manifest must request read-only permissions");
        permissions.Should().NotContain("TIA.ReadWrite", "MVP Add-In manifest must not request read-write permissions");

        var unrestricted = document.Descendants().Where(e => e.Name.LocalName == "UnrestrictedAccess");
        unrestricted.Should().BeEmpty("MVP Add-In manifest must not grant UnrestrictedAccess");
    }

    [Fact]
    public void Release_runner_documentation_exists_and_contains_required_sections()
    {
        var root = FindRepositoryRoot();
        var docPath = Path.Combine(root, "docs", "RELEASE_RUNNER.md");

        File.Exists(docPath).Should().BeTrue("docs/RELEASE_RUNNER.md must exist to document Windows release runner architecture");

        var content = File.ReadAllText(docPath);
        content.Should().Contain("Runner Identity and Labels", "RELEASE_RUNNER.md must document runner labels");
        content.Should().Contain("Prerequisites", "RELEASE_RUNNER.md must document hardware and software prerequisites");
        content.Should().Contain("Security Model", "RELEASE_RUNNER.md must document account and secret security model");
        content.Should().Contain("Job and Workspace Isolation", "RELEASE_RUNNER.md must document job isolation and workspace sanitization");
        content.Should().Contain("Maintenance and Disaster Recovery", "RELEASE_RUNNER.md must document maintenance and disaster recovery procedures");
    }

    [Fact]
    public void CI_workflow_isolates_pull_requests_from_self_hosted_release_runners()
    {
        var root = FindRepositoryRoot();
        var ciWorkflowPath = Path.Combine(root, ".github", "workflows", "ci.yml");

        File.Exists(ciWorkflowPath).Should().BeTrue(".github/workflows/ci.yml must exist");

        var content = File.ReadAllText(ciWorkflowPath);
        content.Should().NotContain("release-runner", "PR CI workflow must not consume self-hosted release runners");
        content.Should().NotContain("self-hosted", "PR CI workflow must not consume self-hosted runners");
        content.Should().Contain("windows-latest", "PR CI workflow must use GitHub-hosted windows-latest runner");
    }

    [Fact]
    public void Release_runner_provisioning_script_exists()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "scripts", "runner", "provision-release-runner.ps1");

        File.Exists(scriptPath).Should().BeTrue("scripts/runner/provision-release-runner.ps1 must exist to automate runner verification");

        var content = File.ReadAllText(scriptPath);
        content.Should().Contain("Check-Environment", "Provisioning script must validate runner environment");
        content.Should().Contain("Sanitize-WorkspaceEnvironment", "Provisioning script must support workspace sanitization");
    }

    [Fact]
    public void Release_workflow_exists_and_automates_addin_packaging_on_release_runner()
    {
        var root = FindRepositoryRoot();
        var releaseWorkflowPath = Path.Combine(root, ".github", "workflows", "release.yml");

        File.Exists(releaseWorkflowPath).Should().BeTrue(".github/workflows/release.yml must exist for consolidated release packaging");

        var content = File.ReadAllText(releaseWorkflowPath);
        content.Should().Contain("release-runner", "Release workflow must run on self-hosted release runner");
        content.Should().Contain("self-hosted", "Release workflow must target self-hosted runners");
        content.Should().Contain("tia-v21", "Release workflow must target runners with TIA Portal V21");
        content.Should().Contain("pack-addin", "Release workflow must automate Add-In packaging");
        content.Should().Contain("verify-addin", "Release workflow must verify the Add-In package");
        content.Should().Contain("provision-release-runner.ps1", "Release workflow must sanitize the release runner workspace");
        content.Should().Contain("v[0-9]+", "Release workflow must trigger on version tag pushes");
    }

    [Fact]
    public void Release_workflow_enforces_mandatory_release_signing_and_signature_verification()
    {
        var root = FindRepositoryRoot();
        var releaseWorkflowPath = Path.Combine(root, ".github", "workflows", "release.yml");

        File.Exists(releaseWorkflowPath).Should().BeTrue(".github/workflows/release.yml must exist");

        var content = File.ReadAllText(releaseWorkflowPath);
        content.Should().Contain("TIA_REQUIRE_SIGNING", "Release workflow must set TIA_REQUIRE_SIGNING environment variable");
        content.Should().Contain("-RequireSigning", "Release workflow must pass -RequireSigning to build.ps1");
        content.Should().Contain("TIA_SIGNING_CERT_PFX_BASE64", "Release workflow must inject PFX base64 secret");
        content.Should().Contain("TIA_SIGNING_CERT_PASSWORD", "Release workflow must inject PFX password secret");
    }

    [Fact]
    public void Release_signing_documentation_exists_and_covers_required_topics()
    {
        var root = FindRepositoryRoot();
        var signingDocPath = Path.Combine(root, "docs", "SIGNING.md");

        File.Exists(signingDocPath).Should().BeTrue("docs/SIGNING.md must exist");

        var content = File.ReadAllText(signingDocPath);
        content.Should().Contain("Mandatory Signing", "SIGNING.md must define mandatory signing policy");
        content.Should().Contain("Automated Signature Verification", "SIGNING.md must document signature verification");
        content.Should().Contain("Secret Management", "SIGNING.md must document secret handling");
        content.Should().Contain("Certificate Rotation Procedure", "SIGNING.md must document certificate rotation");
        content.Should().Contain("Development vs. Release", "SIGNING.md must document separation of development and release packaging");
    }

    [Fact]
    public void Release_runner_docs_use_windows_svc_cmd_not_svc_sh()
    {
        var root = FindRepositoryRoot();
        var docPath = Path.Combine(root, "docs", "RELEASE_RUNNER.md");

        File.Exists(docPath).Should().BeTrue("docs/RELEASE_RUNNER.md must exist");

        var content = File.ReadAllText(docPath);
        content.Should().NotContain("svc.sh",
            "RELEASE_RUNNER.md must not reference svc.sh (Linux) — use svc.cmd (Windows) for the self-hosted runner");
        content.Should().Contain("svc.cmd",
            "RELEASE_RUNNER.md must document Windows svc.cmd service commands");
    }

    [Fact]
    public void Provisioning_script_references_correct_tia_addin_publisher_path()
    {
        var root = FindRepositoryRoot();
        var scriptPath = Path.Combine(root, "scripts", "runner", "provision-release-runner.ps1");

        File.Exists(scriptPath).Should().BeTrue("scripts/runner/provision-release-runner.ps1 must exist");

        var content = File.ReadAllText(scriptPath);
        content.Should().NotContain("V21.AddIn",
            "Provisioning script must not reference non-existent V21.AddIn subdirectory for the Add-In Publisher");
        content.Should().Contain("AddIn.Publisher",
            "Provisioning script must reference the Siemens Add-In Publisher executable");
    }

    [Fact]
    public void Release_workflow_validates_signing_secrets_before_build()
    {
        var root = FindRepositoryRoot();
        var workflowPath = Path.Combine(root, ".github", "workflows", "release.yml");

        File.Exists(workflowPath).Should().BeTrue(".github/workflows/release.yml must exist");

        var content = File.ReadAllText(workflowPath);
        content.Should().Contain("Validate signing secrets",
            "Release workflow must validate signing secrets before expensive build operations");
        content.Should().Contain("TIA_SIGNING_CERT_PFX_BASE64",
            "Release workflow must check for TIA_SIGNING_CERT_PFX_BASE64");
        content.Should().Contain("TIA_SIGNING_CERT_PASSWORD",
            "Release workflow must check for TIA_SIGNING_CERT_PASSWORD");
        content.Should().Contain("TIA_SIGNING_CERT_THUMBPRINT",
            "Release workflow must check for TIA_SIGNING_CERT_THUMBPRINT");
    }

    [Fact]
    public void Release_workflow_uses_nuget_trusted_publishing_via_oidc()
    {
        var root = FindRepositoryRoot();
        var workflowPath = Path.Combine(root, ".github", "workflows", "release.yml");

        File.Exists(workflowPath).Should().BeTrue(".github/workflows/release.yml must exist");

        var content = File.ReadAllText(workflowPath);
        content.Should().Contain("id-token: write",
            "Release workflow must request id-token:write permission for NuGet trusted publishing");
        // NUGET_API_KEY is allowed as a fallback for first-time package registration.
        // Once the package exists on nuget.org and trusted publishing is configured,
        // this should be removed in favor of pure OIDC.
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
