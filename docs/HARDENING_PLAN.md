# Installation, Update, and Failure Recovery Hardening

This document defines the hardening plan for resolving reliability gaps and proving recovery from interrupted or hostile installation states.

> [!CAUTION]
> This project is experimental and not ready for production use. Do not use it on live systems, safety programs, or workflows where an incorrect response or modification could affect people, equipment, availability, or compliance.

## Objective

Resolve reliability gaps found during beta publication and prove recovery from interrupted or hostile installation states.

## Scope

### Failure Modes to Address

1. **TIA Portal open during update** - Handle locked files and active processes
2. **Locked or in-use Add-In files** - Produce actionable instructions
3. **Active Bridge/runtime processes** - Graceful shutdown before update
4. **Insufficient disk space** - Detect and report before extraction
5. **Interrupted extraction** - Atomic staging and cleanup
6. **Corrupted manifests** - Detect and recover without data loss
7. **PID reuse** - Validate process ownership before shutdown
8. **Incompatible configuration** - Detect and migrate or reject
9. **Failed health checks** - Automatic rollback to previous version
10. **Incomplete rollback** - Verify rollback completed successfully

### Hardening Areas

1. **Transactional staging** - Atomic file operations with rollback
2. **Cleanup and diagnostics** - Improved logging and support information
3. **Regression tests** - Tests for every confirmed beta failure mode
4. **Active version guarantee** - Verify valid version remains after failures

## Implementation Plan

### Phase 1: Transactional Staging

Implement atomic file operations for installation and update:

1. **Staging directory** - Extract to temporary location first
2. **Validation** - Verify extracted files before activation
3. **Atomic replacement** - Move files into place atomically
4. **Rollback on failure** - Restore previous state if activation fails

### Phase 2: Process Management

Improve process lifecycle management:

1. **Active process detection** - Check for running Bridge/runtime processes
2. **Graceful shutdown** - Send shutdown signal before update
3. **Timeout enforcement** - Force kill after graceful period
4. **PID validation** - Verify process ownership before termination

### Phase 3: File Locking

Handle locked or in-use files:

1. **Lock detection** - Detect when files are locked by TIA Portal
2. **Actionable instructions** - Provide clear instructions to user
3. **Retry mechanism** - Retry after user closes TIA Portal
4. **Partial update recovery** - Handle case where some files are locked

### Phase 4: Manifest Integrity

Improve manifest handling:

1. **Checksum validation** - Verify manifest integrity on read
2. **Backup manifests** - Keep backup of previous manifest
3. **Corruption recovery** - Rebuild manifest from installed files
4. **Atomic updates** - Update manifests atomically

### Phase 5: Health Checks and Rollback

Implement automatic recovery:

1. **Health check validation** - Run health checks after activation
2. **Automatic rollback** - Rollback if health checks fail
3. **Rollback verification** - Verify rollback completed successfully
4. **State preservation** - Preserve user configuration during rollback

### Phase 6: Regression Tests

Add tests for every failure mode:

1. **Unit tests** - Test individual components in isolation
2. **Integration tests** - Test complete workflows
3. **Failure injection tests** - Simulate failures and verify recovery
4. **Stress tests** - Test under load and concurrent access

## Acceptance Criteria Verification

- [ ] No tested failure leaves the product without a valid active version
- [ ] Operations are safely repeatable after interruption
- [ ] Locked or in-use Add-In files produce actionable instructions
- [ ] Automatic rollback is tested for activation and health-check failures
- [ ] Corrupt state is detected without destroying recoverable user configuration
- [ ] All beta blockers are closed or explicitly deferred with justification before completion

## Implementation Details

### Transactional Staging Implementation

```csharp
public static class TransactionalStaging
{
    public static async Task StageAndActivate(string sourceDir, string targetDir)
    {
        var stagingDir = Path.Combine(Path.GetTempPath(), "tia-agent-staging-" + Guid.NewGuid());
        
        try
        {
            // Stage files to temporary location
            await StageFiles(sourceDir, stagingDir);
            
            // Validate staged files
            await ValidateStagedFiles(stagingDir);
            
            // Atomic replacement
            await ActivateStagedFiles(stagingDir, targetDir);
        }
        catch (Exception)
        {
            // Cleanup staging directory
            CleanupStaging(stagingDir);
            throw;
        }
    }
}
```

### Process Management Implementation

```csharp
public static class ProcessManager
{
    public static async Task ShutdownProcesses(TimeSpan timeout)
    {
        var processes = FindTiaAgentProcesses();
        
        foreach (var process in processes)
        {
            // Send graceful shutdown signal
            process.CloseMainWindow();
            
            // Wait for exit with timeout
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                // Force kill after timeout
                process.Kill();
            }
        }
    }
}
```

### Health Check Implementation

```csharp
public static class HealthCheck
{
    public static async Task<bool> ValidateAfterActivation()
    {
        try
        {
            // Check if Bridge is responding
            var healthResponse = await CheckBridgeHealth();
            
            // Check if runtime is available
            var runtimeAvailable = await CheckRuntimeAvailability();
            
            // Check if configuration is valid
            var configValid = await ValidateConfiguration();
            
            return healthResponse && runtimeAvailable && configValid;
        }
        catch
        {
            return false;
        }
    }
}
```

## Regression Test Matrix

| Failure Mode | Test Type | Expected Behavior |
|---|---|---|
| TIA Portal open during update | Integration | Actionable instructions provided |
| Locked Add-In files | Unit | Retry mechanism activates |
| Active Bridge process | Integration | Graceful shutdown before update |
| Insufficient disk space | Unit | Error reported before extraction |
| Interrupted extraction | Integration | Staging directory cleaned up |
| Corrupted manifest | Unit | Backup manifest restored |
| PID reuse | Unit | Process ownership validated |
| Incompatible configuration | Unit | Migration or rejection occurs |
| Failed health check | Integration | Automatic rollback triggered |
| Incomplete rollback | Integration | Rollback verified complete |

## Documentation Updates

After hardening implementation:

1. Update `docs/TROUBLESHOOTING.md` with new failure modes
2. Update `docs/INSTALLATION.md` with updated procedures
3. Update `docs/UPDATING.md` with updated procedures
4. Update `docs/ROLLBACK.md` with updated procedures
5. Create release notes documenting hardening improvements

## Next Steps

After hardening implementation:

1. Execute regression test matrix
2. Document any remaining limitations
3. Proceed to RC publication (Issue #57)
