using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;

namespace TiaAgent.Simulator;

/// <summary>
/// Pre-loaded sample data for the DemoConveyorLine project.
/// </summary>
public class SimulatorData
{
    public TiaContextDto Context { get; }
    public List<BlockSummaryDto> Blocks { get; }
    public Dictionary<string, BlockDetail> BlockDetails { get; }
    public Dictionary<string, CallHierarchyDto> CallHierarchies { get; }
    public List<ReferenceDto> References { get; }
    public Dictionary<string, SelectionSnapshotDto> Selections { get; } = new();

    public SimulatorData(IContentHashService hashService, IClock clock)
    {
        Context = new TiaContextDto
        {
            TiaVersion = "V21",
            OpennessVersion = "V21",
            TiaSessionId = "tia-sim-001",
            ProjectId = "sim-project-001",
            ProjectName = "DemoConveyorLine",
            ProjectPath = "C:\\Projects\\DemoConveyorLine\\DemoConveyorLine.ap17",
            PlcCount = 1,
            BlockCount = 6,
            LastModified = clock.UtcNow,
            Capabilities = new CapabilityDto
            {
                CaptureSelection = true,
                ListBlocks = true,
                ReadBlockSource = true,
                ReadBlockInterface = true,
                FindReferences = true,
                CompileSoftware = true,
                PreviewBlockChange = true,
                ImportBlock = false,
                HardwareWrites = false,
                SafetyWrites = false,
                DownloadToPlc = false
            }
        };

        Blocks = new List<BlockSummaryDto>
        {
            CreateBlock("block-ob-main", "OB_Main", "OrganizationBlock", "PLC_1/Program blocks/OB_Main", "STL", "plc-001", hashService, clock),
            CreateBlock("block-fb-conveyor", "FB_Conveyor", "FunctionBlock", "PLC_1/Program blocks/FB_Conveyor", "SCL", "plc-001", hashService, clock),
            CreateBlock("block-fb-motor", "FB_Motor", "FunctionBlock", "PLC_1/Program blocks/FB_Motor", "SCL", "plc-001", hashService, clock),
            CreateBlock("block-fb-safety", "FB_SafetyInterlock", "FunctionBlock", "PLC_1/Program blocks/FB_SafetyInterlock", "SCL", "plc-001", hashService, clock),
            CreateBlock("block-db-conveyor", "DB_Conveyor", "GlobalDataBlock", "PLC_1/Program blocks/DB_Conveyor", "SCL", "plc-001", hashService, clock),
            CreateBlock("block-fc-alarm", "FC_AlarmHandler", "Function", "PLC_1/Program blocks/FC_AlarmHandler", "LAD", "plc-001", hashService, clock)
        };

        BlockDetails = CreateBlockDetails(hashService);
        CallHierarchies = CreateCallHierarchies();
        References = CreateReferences();
    }

    private static BlockSummaryDto CreateBlock(string id, string name, string type, string path, string language, string plcId, IContentHashService hash, IClock clock)
    {
        return new BlockSummaryDto
        {
            ObjectId = id,
            Name = name,
            BlockType = type,
            Path = path,
            Language = language,
            PlcId = plcId,
            ContentHash = hash.ComputeHash($"block-{name}"),
            LastObservedAt = clock.UtcNow
        };
    }

    private static Dictionary<string, BlockDetail> CreateBlockDetails(IContentHashService hash)
    {
        return new Dictionary<string, BlockDetail>
        {
            ["block-ob-main"] = new BlockDetail
            {
                SourceCode = @"// Organization Block: Main Program Cycle
// This is the main cyclic execution block.

NETWORK 1 // Call conveyor control
      A     ""StartCommand""
      JNB   _001
      CALL  ""FB_Conveyor""  DB_Conveyor
(
      Enable    := ""StartCommand"",
      Speed     := ""ConveyorSpeed""
)
_001: NOP   0

NETWORK 2 // Check safety interlock
      A     ""EmergencyStop""
      JNB   _002
      CALL  ""FB_SafetyInterlock""
_002: NOP   0

NETWORK 3 // Alarm monitoring
      A     ""MotorFault""
      JNB   _003
      CALL  ""FC_AlarmHandler""
_003: NOP   0",
                Interface = CreateOBInterface(),
                ContentHash = hash.ComputeHash("ob-main-source")
            },
            ["block-fb-conveyor"] = new BlockDetail
            {
                SourceCode = @"FUNCTION_BLOCK ""FB_Conveyor""
VAR_INPUT
    Enable : BOOL;           // Enable conveyor operation
    Speed : INT;             // Target speed (0-100%)
    Direction : BOOL;        // TRUE = forward, FALSE = reverse
END_VAR
VAR_OUTPUT
    Running : BOOL;          // TRUE when conveyor is running
    CurrentSpeed : INT;      // Current actual speed
    Fault : BOOL;            // TRUE on fault condition
END_VAR
VAR
    _rampUp : REAL;          // Ramp-up accumulator
    _rampDown : REAL;        // Ramp-down accumulator
    _lastEnable : BOOL;      // Previous cycle enable state
    _faultTimer : TIME;      // Fault detection timer
END_VAR

BEGIN
    // Safety check: Emergency stop immediately stops the conveyor
    IF NOT Enable OR NOT ""EmergencyStop"" THEN
        _rampDown := REAL#100.0;
        _rampUp := REAL#0.0;

        // Ramp down gracefully
        IF _rampDown > REAL#0.0 THEN
            _rampDown := _rampDown - REAL#10.0;
            CurrentSpeed := REAL#ToInt(_rampDown * INT#ToReal(Speed) / REAL#100.0);
        ELSE
            CurrentSpeed := 0;
            Running := FALSE;
        END_IF;

        _lastEnable := FALSE;
        RETURN;
    END_IF;

    // Ramp up on enable
    IF Enable AND NOT _lastEnable THEN
        _rampUp := REAL#0.0;
        _lastEnable := TRUE;
    END_IF;

    IF _rampUp < REAL#100.0 THEN
        _rampUp := _rampUp + REAL#5.0;
        IF _rampUp > REAL#100.0 THEN
            _rampUp := REAL#100.0;
        END_IF;
    END_IF;

    // Apply speed with ramp
    CurrentSpeed := REAL#ToInt(_rampUp * INT#ToReal(Speed) / REAL#100.0);
    Running := CurrentSpeed > 0;

    // Motor fault detection
    IF ""MotorFault"" AND Running THEN
        _faultTimer := _faultTimer + T#100MS;
        IF _faultTimer > T#2S THEN
            Fault := TRUE;
            Running := FALSE;
            CurrentSpeed := 0;
        END_IF;
    ELSE
        _faultTimer := T#0S;
        Fault := FALSE;
    END_IF;
END_FUNCTION_BLOCK",
                Interface = CreateFBConveyorInterface(),
                ContentHash = hash.ComputeHash("fb-conveyor-source")
            },
            ["block-fb-motor"] = new BlockDetail
            {
                SourceCode = @"FUNCTION_BLOCK ""FB_Motor""
VAR_INPUT
    Start : BOOL;
    Stop : BOOL;
    Speed : INT;
    Direction : BOOL;
END_VAR
VAR_OUTPUT
    Running : BOOL;
    CurrentSpeed : INT;
    Overtemp : BOOL;
END_VAR
VAR
    _state : INT;
    _temp : REAL;
END_VAR

BEGIN
    CASE _state OF
        0: // Idle
            IF Start AND NOT Stop THEN
                _state := 1;
            END_IF;
            Running := FALSE;
            CurrentSpeed := 0;

        1: // Running
            Running := TRUE;
            CurrentSpeed := Speed;
            IF Stop THEN
                _state := 0;
            END_IF;

            // Simple temperature monitoring
            _temp := _temp + REAL#0.1;
            IF _temp > REAL#85.0 THEN
                Overtemp := TRUE;
                _state := 2;
            END_IF;

        2: // Fault
            Running := FALSE;
            CurrentSpeed := 0;
            IF NOT Start THEN
                _state := 0;
                _temp := REAL#0.0;
                Overtemp := FALSE;
            END_IF;
    END_CASE;
END_FUNCTION_BLOCK",
                Interface = CreateFBMotorInterface(),
                ContentHash = hash.ComputeHash("fb-motor-source")
            },
            ["block-fb-safety"] = new BlockDetail
            {
                SourceCode = @"FUNCTION_BLOCK ""FB_SafetyInterlock""
VAR_INPUT
    EmergencyStop : BOOL;    // Emergency stop signal
    SafetyGate : BOOL;       // Safety gate closed
    LightCurtain : BOOL;     // Light curtain clear
END_VAR
VAR_OUTPUT
    SafeToOperate : BOOL;    // All safety conditions met
    SafetyFault : BOOL;      // Safety fault detected
    LastFaultCode : INT;     // Last fault code
END_VAR

BEGIN
    // All safety inputs must be TRUE for safe operation
    SafeToOperate := EmergencyStop AND SafetyGate AND LightCurtain;

    IF NOT SafeToOperate THEN
        SafetyFault := TRUE;
        IF NOT EmergencyStop THEN
            LastFaultCode := 1;  // Emergency stop activated
        ELSIF NOT SafetyGate THEN
            LastFaultCode := 2;  // Safety gate open
        ELSIF NOT LightCurtain THEN
            LastFaultCode := 3;  // Light curtain broken
        END_IF;
    ELSE
        SafetyFault := FALSE;
        LastFaultCode := 0;
    END_IF;
END_FUNCTION_BLOCK",
                Interface = CreateFBSafetyInterface(),
                ContentHash = hash.ComputeHash("fb-safety-source")
            },
            ["block-db-conveyor"] = new BlockDetail
            {
                SourceCode = @"DATA_BLOCK ""DB_Conveyor""
VERSION : 0.1
  STRUCT
    TargetSpeed : INT := 50;         // Default speed 50%
    MaxSpeed : INT := 100;           // Maximum allowed speed
    MinSpeed : INT := 10;            // Minimum speed
    CurrentState : INT := 0;         // 0=stopped, 1=running, 2=fault
    RunTime : TIME;                  // Total run time accumulator
    CycleCount : DINT;               // Number of start/stop cycles
    LastStartTime : DATE_AND_TIME;   // Last start timestamp
    LastStopTime : DATE_AND_TIME;    // Last stop timestamp
    OperatorID : STRING(20);         // Operator who last started
  END_STRUCT
END_DATA_BLOCK",
                Interface = CreateDBConveyorInterface(),
                ContentHash = hash.ComputeHash("db-conveyor-source")
            },
            ["block-fc-alarm"] = new BlockDetail
            {
                SourceCode = @"FUNCTION ""FC_AlarmHandler"" : Void
VAR_INPUT
    AlarmTrigger : BOOL;
    AlarmCode : INT;
    Acknowledge : BOOL;
END_VAR
VAR_OUTPUT
    AlarmActive : BOOL;
    AlarmHistory : ARRAY[0..9] OF INT;
END_VAR
VAR_TEMP
    _index : INT;
END_VAR

BEGIN
    IF AlarmTrigger AND NOT Acknowledge THEN
        AlarmActive := TRUE;
        // Log to history (simple circular buffer)
        FOR _index := 0 TO 8 DO
            AlarmHistory[_index] := AlarmHistory[_index + 1];
        END_FOR;
        AlarmHistory[9] := AlarmCode;
    ELSIF Acknowledge THEN
        AlarmActive := FALSE;
    END_IF;
END_FUNCTION",
                Interface = CreateFCAlarmInterface(),
                ContentHash = hash.ComputeHash("fc-alarm-source")
            }
        };
    }

    private static Dictionary<string, CallHierarchyDto> CreateCallHierarchies()
    {
        return new Dictionary<string, CallHierarchyDto>
        {
            ["block-ob-main"] = new CallHierarchyDto
            {
                RootObjectId = "block-ob-main",
                RootName = "OB_Main",
                Nodes = new List<CallHierarchyNodeDto>
                {
                    new()
                    {
                        ObjectId = "block-fb-conveyor",
                        Name = "FB_Conveyor",
                        BlockType = "FunctionBlock",
                        Path = "PLC_1/Program blocks/FB_Conveyor",
                        Children = new List<CallHierarchyNodeDto>
                        {
                            new() { ObjectId = "block-fb-motor", Name = "FB_Motor", BlockType = "FunctionBlock", Children = Array.Empty<CallHierarchyNodeDto>() },
                            new() { ObjectId = "block-fb-safety", Name = "FB_SafetyInterlock", BlockType = "FunctionBlock", Children = Array.Empty<CallHierarchyNodeDto>() },
                            new() { ObjectId = "block-fc-alarm", Name = "FC_AlarmHandler", BlockType = "Function", Children = Array.Empty<CallHierarchyNodeDto>() }
                        }
                    }
                }
            }
        };
    }

    private static List<ReferenceDto> CreateReferences()
    {
        return new List<ReferenceDto>
        {
            new() { SourceObjectId = "block-ob-main", SourceName = "OB_Main", TargetObjectId = "block-db-conveyor", TargetName = "DB_Conveyor", ReferenceType = "DataBlock", Location = "NETWORK 1" },
            new() { SourceObjectId = "block-ob-main", SourceName = "OB_Main", TargetObjectId = "block-fb-conveyor", TargetName = "FB_Conveyor", ReferenceType = "FunctionBlock", Location = "NETWORK 1" },
            new() { SourceObjectId = "block-fb-conveyor", SourceName = "FB_Conveyor", TargetObjectId = "block-fb-motor", TargetName = "FB_Motor", ReferenceType = "FunctionBlock", Location = "Line 45" },
            new() { SourceObjectId = "block-fb-conveyor", SourceName = "FB_Conveyor", TargetObjectId = "block-fb-safety", TargetName = "FB_SafetyInterlock", ReferenceType = "FunctionBlock", Location = "Line 46" },
            new() { SourceObjectId = "block-fb-conveyor", SourceName = "FB_Conveyor", TargetObjectId = "block-fc-alarm", TargetName = "FC_AlarmHandler", ReferenceType = "Function", Location = "Line 47" },
            new() { SourceObjectId = "block-fb-conveyor", SourceName = "FB_Conveyor", TargetObjectId = "block-db-conveyor", TargetName = "DB_Conveyor", ReferenceType = "DataBlock", Location = "Line 23" },
            new() { SourceObjectId = "block-ob-main", SourceName = "OB_Main", TargetObjectId = "block-fb-safety", TargetName = "FB_SafetyInterlock", ReferenceType = "FunctionBlock", Location = "NETWORK 2" },
            new() { SourceObjectId = "block-ob-main", SourceName = "OB_Main", TargetObjectId = "block-fc-alarm", TargetName = "FC_AlarmHandler", ReferenceType = "Function", Location = "NETWORK 3" }
        };
    }

    private static BlockInterfaceDto CreateOBInterface() => new()
    {
        ObjectId = "block-ob-main",
        Name = "OB_Main",
        InputParams = Array.Empty<InterfaceParameterDto>(),
        OutputParams = Array.Empty<InterfaceParameterDto>(),
        InOutParams = Array.Empty<InterfaceParameterDto>(),
        StaticVars = Array.Empty<InterfaceParameterDto>(),
        TempVars = Array.Empty<InterfaceParameterDto>()
    };

    private static BlockInterfaceDto CreateFBConveyorInterface() => new()
    {
        ObjectId = "block-fb-conveyor",
        Name = "FB_Conveyor",
        InputParams = new List<InterfaceParameterDto>
        {
            new() { Name = "Enable", DataType = "BOOL", Comment = "Enable conveyor operation" },
            new() { Name = "Speed", DataType = "INT", Comment = "Target speed (0-100%)" },
            new() { Name = "Direction", DataType = "BOOL", Comment = "TRUE = forward, FALSE = reverse" }
        },
        OutputParams = new List<InterfaceParameterDto>
        {
            new() { Name = "Running", DataType = "BOOL", Comment = "TRUE when conveyor is running" },
            new() { Name = "CurrentSpeed", DataType = "INT", Comment = "Current actual speed" },
            new() { Name = "Fault", DataType = "BOOL", Comment = "TRUE on fault condition" }
        },
        InOutParams = Array.Empty<InterfaceParameterDto>(),
        StaticVars = new List<InterfaceParameterDto>
        {
            new() { Name = "_rampUp", DataType = "REAL", Comment = "Ramp-up accumulator" },
            new() { Name = "_rampDown", DataType = "REAL", Comment = "Ramp-down accumulator" },
            new() { Name = "_lastEnable", DataType = "BOOL", Comment = "Previous cycle enable state" },
            new() { Name = "_faultTimer", DataType = "TIME", Comment = "Fault detection timer" }
        },
        TempVars = Array.Empty<InterfaceParameterDto>()
    };

    private static BlockInterfaceDto CreateFBMotorInterface() => new()
    {
        ObjectId = "block-fb-motor",
        Name = "FB_Motor",
        InputParams = new List<InterfaceParameterDto>
        {
            new() { Name = "Start", DataType = "BOOL" },
            new() { Name = "Stop", DataType = "BOOL" },
            new() { Name = "Speed", DataType = "INT" },
            new() { Name = "Direction", DataType = "BOOL" }
        },
        OutputParams = new List<InterfaceParameterDto>
        {
            new() { Name = "Running", DataType = "BOOL" },
            new() { Name = "CurrentSpeed", DataType = "INT" },
            new() { Name = "Overtemp", DataType = "BOOL" }
        },
        InOutParams = Array.Empty<InterfaceParameterDto>(),
        StaticVars = new List<InterfaceParameterDto>
        {
            new() { Name = "_state", DataType = "INT" },
            new() { Name = "_temp", DataType = "REAL" }
        },
        TempVars = Array.Empty<InterfaceParameterDto>()
    };

    private static BlockInterfaceDto CreateFBSafetyInterface() => new()
    {
        ObjectId = "block-fb-safety",
        Name = "FB_SafetyInterlock",
        InputParams = new List<InterfaceParameterDto>
        {
            new() { Name = "EmergencyStop", DataType = "BOOL" },
            new() { Name = "SafetyGate", DataType = "BOOL" },
            new() { Name = "LightCurtain", DataType = "BOOL" }
        },
        OutputParams = new List<InterfaceParameterDto>
        {
            new() { Name = "SafeToOperate", DataType = "BOOL" },
            new() { Name = "SafetyFault", DataType = "BOOL" },
            new() { Name = "LastFaultCode", DataType = "INT" }
        },
        InOutParams = Array.Empty<InterfaceParameterDto>(),
        StaticVars = Array.Empty<InterfaceParameterDto>(),
        TempVars = Array.Empty<InterfaceParameterDto>()
    };

    private static BlockInterfaceDto CreateDBConveyorInterface() => new()
    {
        ObjectId = "block-db-conveyor",
        Name = "DB_Conveyor",
        InputParams = Array.Empty<InterfaceParameterDto>(),
        OutputParams = Array.Empty<InterfaceParameterDto>(),
        InOutParams = Array.Empty<InterfaceParameterDto>(),
        StaticVars = new List<InterfaceParameterDto>
        {
            new() { Name = "TargetSpeed", DataType = "INT", DefaultValue = "50" },
            new() { Name = "MaxSpeed", DataType = "INT", DefaultValue = "100" },
            new() { Name = "MinSpeed", DataType = "INT", DefaultValue = "10" },
            new() { Name = "CurrentState", DataType = "INT", DefaultValue = "0" },
            new() { Name = "RunTime", DataType = "TIME" },
            new() { Name = "CycleCount", DataType = "DINT" },
            new() { Name = "LastStartTime", DataType = "DATE_AND_TIME" },
            new() { Name = "LastStopTime", DataType = "DATE_AND_TIME" },
            new() { Name = "OperatorID", DataType = "STRING(20)" }
        },
        TempVars = Array.Empty<InterfaceParameterDto>()
    };

    private static BlockInterfaceDto CreateFCAlarmInterface() => new()
    {
        ObjectId = "block-fc-alarm",
        Name = "FC_AlarmHandler",
        InputParams = new List<InterfaceParameterDto>
        {
            new() { Name = "AlarmTrigger", DataType = "BOOL" },
            new() { Name = "AlarmCode", DataType = "INT" },
            new() { Name = "Acknowledge", DataType = "BOOL" }
        },
        OutputParams = new List<InterfaceParameterDto>
        {
            new() { Name = "AlarmActive", DataType = "BOOL" },
            new() { Name = "AlarmHistory", DataType = "ARRAY[0..9] OF INT" }
        },
        InOutParams = Array.Empty<InterfaceParameterDto>(),
        StaticVars = Array.Empty<InterfaceParameterDto>(),
        TempVars = new List<InterfaceParameterDto>
        {
            new() { Name = "_index", DataType = "INT" }
        }
    };
}

public class BlockDetail
{
    public string? SourceCode { get; init; }
    public BlockInterfaceDto? Interface { get; init; }
    public string? ContentHash { get; init; }
}
