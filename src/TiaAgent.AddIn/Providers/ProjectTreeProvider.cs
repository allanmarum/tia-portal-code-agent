#if SIEMENS
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using Siemens.Engineering;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;
using TiaAgent.AddIn.Ui;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Errors;

namespace TiaAgent.AddIn.Providers;

public sealed class ProjectTreeProvider : ProjectTreeAddInProvider
{
    protected override IEnumerable<ContextMenuAddIn> GetContextMenuAddIns()
    {
        yield return new TiaAgentContextMenu();
    }
}

public sealed class TiaAgentContextMenu : ContextMenuAddIn
{
    private const string McpEndpoint = "http://127.0.0.1:43121/mcp";

    public TiaAgentContextMenu() : base("AI Assistant") { }

    protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRoot)
    {
        var aiSubmenu = addInRoot.Items.AddSubmenu("AI Assistant");

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Explain selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("explain", "Explain this PLC block in detail", selection));

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Review selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("review", "Review this PLC code for defects and improvements", selection));

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Propose change",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("propose", "Propose a change to this PLC block", selection));
    }

    private void HandleAction(string action, string taskDescription, MenuSelectionProvider<IEngineeringObject> selection)
    {
        try
        {
            Log("HandleAction: " + action);

            var objects = selection.GetSelection();
            var enumerator = objects.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                AssistantPanelFactory.ShowWarning("No object selected.");
                return;
            }

            var selectedObj = enumerator.Current;
            var objName = selectedObj.ToString() ?? "Unknown";
            var objType = selectedObj.GetType().Name;
            Log("Selected: " + objName + " (" + objType + ")");

            // Use orchestrator for OpenCode, fallback to direct MCP
            string result;
            var orchestrator = AddInServices.Orchestrator;
            if (orchestrator.IsOpenCodeAvailableAsync(CancellationToken.None).GetAwaiter().GetResult())
            {
                Log("Using OpenCode agent runtime via orchestrator");
                result = RunViaOrchestrator(orchestrator, action, taskDescription, objName, objType);
            }
            else
            {
                Log("OpenCode not available, using direct MCP");
                result = RunViaMcp(objName, objType);
            }

            Log("Result length: " + result.Length);
            AssistantPanelFactory.ShowResult(action, result);
        }
        catch (TiaErrorException ex)
        {
            Log("TIA ERROR: " + ex.Error.Code + " - " + ex.Error.Message);
            var userMessage = ErrorMapper.ToUserMessage(ex.Error);
            AssistantPanelFactory.ShowError(userMessage);
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.ToString());
            var error = new TiaError { Code = TiaErrorCode.INTERNAL_ERROR, Message = ex.Message };
            var userMessage = ErrorMapper.ToUserMessage(error);
            AssistantPanelFactory.ShowError(userMessage);
        }
    }

    private string RunViaOrchestrator(
        IOpenCodeOrchestrator orchestrator,
        string action,
        string taskDescription,
        string objName,
        string objType)
    {
        var correlationId = "tia-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var message = taskDescription + " - Object: " + objName + " (" + objType + ")";

        var descriptor = new OpenCodeTaskDescriptor
        {
            Action = action,
            Message = message,
            CorrelationId = correlationId,
            TiaSessionId = "tia-session",
            ProjectId = "tia-project",
            AgentId = "tia-explain"
        };

        var result = orchestrator.ExecuteTaskAsync(descriptor, CancellationToken.None)
            .GetAwaiter().GetResult();

        if (result.Success)
        {
            return result.Response ?? "Task completed (no output)";
        }

        // Map error to user-friendly message
        if (!string.IsNullOrEmpty(result.ErrorCode) &&
            Enum.TryParse<TiaErrorCode>(result.ErrorCode, out var errorCode))
        {
            var error = new TiaError { Code = errorCode, Message = result.Error ?? "Unknown error" };
            return ErrorMapper.ToUserMessage(error);
        }

        return result.Error ?? "An unexpected error occurred.";
    }

    private string RunViaMcp(string objName, string objType)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var sessionId = InitializeMcpSession(client);
        var response = CallMcpTool(client, sessionId, "tia_get_current_context", "{}");
        return "[MCP Direct] Object: " + objName + " (" + objType + ")\n\nContext:\n" + response;
    }

    private string InitializeMcpSession(HttpClient client)
    {
        var initBody = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-03-26\",\"capabilities\":{},\"clientInfo\":{\"name\":\"tia-addin\",\"version\":\"0.1.0\"}}}";
        var initReq = new HttpRequestMessage(HttpMethod.Post, McpEndpoint)
        {
            Content = new StringContent(initBody, Encoding.UTF8, "application/json")
        };
        initReq.Headers.Add("Accept", "application/json, text/event-stream");
        var initResp = client.SendAsync(initReq).GetAwaiter().GetResult();
        var sessionId = initResp.Headers.Contains("Mcp-Session-Id")
            ? string.Join("", initResp.Headers.GetValues("Mcp-Session-Id"))
            : "";

        var notifBody = "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}";
        var notifReq = new HttpRequestMessage(HttpMethod.Post, McpEndpoint)
        {
            Content = new StringContent(notifBody, Encoding.UTF8, "application/json")
        };
        notifReq.Headers.Add("Accept", "application/json, text/event-stream");
        notifReq.Headers.Add("Mcp-Session-Id", sessionId);
        client.SendAsync(notifReq).GetAwaiter().GetResult();
        return sessionId;
    }

    private string CallMcpTool(HttpClient client, string sessionId, string toolName, string args)
    {
        var body = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"" + toolName + "\",\"arguments\":" + args + "}}";
        var req = new HttpRequestMessage(HttpMethod.Post, McpEndpoint)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Accept", "application/json, text/event-stream");
        req.Headers.Add("Mcp-Session-Id", sessionId);
        var resp = client.SendAsync(req).GetAwaiter().GetResult();
        var content = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        foreach (var line in content.Split('\n'))
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                return line.Substring(6);
            }
        }
        return content;
    }

    private void Log(string message)
    {
        try
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TiaAgent", "debug.log");
            var dir = System.IO.Path.GetDirectoryName(logPath);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine);
        }
        catch { }
    }
}
#endif
