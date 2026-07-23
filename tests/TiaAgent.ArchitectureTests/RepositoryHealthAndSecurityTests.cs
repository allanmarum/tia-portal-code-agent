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
