using Mono.Debugging.Client;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using NewtonConverter = Newtonsoft.Json.JsonConvert;
using DebugProtocol = Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Mono.Debugging.Soft;
using System.Text.Json.Serialization;
using System.Text;
using DotRush.Common.Logging;

namespace DotRush.Debugging.Mono.Extensions;

public static class ServerExtensions {
    public static DebuggerSessionOptions DefaultDebuggerOptions { get; } = new DebuggerSessionOptions {
        EvaluationOptions = new EvaluationOptions {
            EvaluationTimeout = 1000,
            MemberEvaluationTimeout = 5000,
            UseExternalTypeResolver = true,
            AllowMethodEvaluation = true,
            GroupPrivateMembers = true,
            GroupStaticMembers = true,
            AllowToStringCalls = true,
            AllowTargetInvoke = true,
            EllipsizeStrings = true,
            EllipsizedLength = 260,
            CurrentExceptionTag = "$exception",
            IntegerDisplayFormat = IntegerDisplayFormat.Decimal,
            StackFrameFormat = new StackFrameFormat()
        },
        ProjectAssembliesOnly = true
    };
    public static JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };


    public static ProtocolException GetProtocolException(string message) {
        return new ProtocolException(message, 0, message, url: $"file://{LogConfig.DebugLogFile}");
    }
    public static int GetSourceReference(this StackFrame frame) {
        var key = frame.SourceLocation.MethodName ?? "null";
        if (!string.IsNullOrEmpty(frame.SourceLocation.FileName)) {
            key = frame.SourceLocation.FileName;
            if (frame.SourceLocation.FileName.Contains(".g.cs", StringComparison.OrdinalIgnoreCase))
                key = $"{frame.SourceLocation.FileName}:{frame.SourceLocation.Line}";
        }
        return Math.Abs(key.GetHashCode());
    }
    public static string? TrimExpression(this DebugProtocol.EvaluateArguments args) {
        return args.Expression?.TrimEnd(';')?.Replace("?.", ".");
    }

    public static T DoSafe<T>(Func<T> handler, Action? finalizer = null) {
        try {
            return handler.Invoke();
        } catch (Exception ex) {
            finalizer?.Invoke();
            if (ex is ProtocolException)
                throw;
            DebuggerLoggingService.CustomLogger?.LogError($"[Handled] {ex.Message}", ex);
            throw GetProtocolException(ex.Message);
        }
    }

    public static JToken? TryGetValue(this Dictionary<string, JToken> dictionary, string key) {
        if (dictionary.TryGetValue(key, out var value))
            return value;
        return null;
    }
    public static T? ToClass<T>(this JToken? jtoken) where T : class {
        if (jtoken == null)
            return default;

        string json = NewtonConverter.SerializeObject(jtoken);
        if (string.IsNullOrEmpty(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }
    public static T ToValue<T>(this JToken? jtoken) where T : struct {
        if (jtoken == null)
            return default;

        string json = NewtonConverter.SerializeObject(jtoken);
        if (string.IsNullOrEmpty(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }
    public static DebugProtocol.CompletionItem ToCompletionItem(this CompletionItem item) {
        return new DebugProtocol.CompletionItem() {
            Type = item.Flags.ToCompletionItemType(),
            SortText = item.Name,
            Label = item.Name,
        };
    }
    private static DebugProtocol.CompletionItemType ToCompletionItemType(this ObjectValueFlags flags) {
        if (flags.HasFlag(ObjectValueFlags.Method))
            return DebugProtocol.CompletionItemType.Method;
        if (flags.HasFlag(ObjectValueFlags.Field))
            return DebugProtocol.CompletionItemType.Field;
        if (flags.HasFlag(ObjectValueFlags.Property))
            return DebugProtocol.CompletionItemType.Property;
        if (flags.HasFlag(ObjectValueFlags.Namespace))
            return DebugProtocol.CompletionItemType.Module;
        if (flags.HasFlag(ObjectValueFlags.Type))
            return DebugProtocol.CompletionItemType.Class;
        if (flags.HasFlag(ObjectValueFlags.Variable))
            return DebugProtocol.CompletionItemType.Variable;

        return DebugProtocol.CompletionItemType.Text;
    }
    public static DebugProtocol.Breakpoint ToBreakpoint(this Breakpoint breakpoint, SoftDebuggerSession session) {
        return new DebugProtocol.Breakpoint() {
            Id = breakpoint.GetHashCode(),
            Verified = breakpoint.GetStatus(session) == BreakEventStatus.Bound,
            Message = breakpoint.GetStatusMessage(session),
            Line = breakpoint.Line,
            Column = breakpoint.Column,
        };
    }
    public static DebugProtocol.Module ToModule(this Assembly assembly) {
        return new DebugProtocol.Module {
            Id = assembly.Name.GetHashCode(),
            Name = assembly.Name,
            Path = assembly.Path,
            IsOptimized = assembly.Optimized,
            IsUserCode = assembly.UserCode,
            Version = assembly.Version,
            SymbolFilePath = assembly.SymbolFile,
            DateTimeStamp = assembly.TimeStamp,
            AddressRange = assembly.Address,
            SymbolStatus = assembly.SymbolStatus,
            VsAppDomain = assembly.AppDomain,
        };
    }
    public static DebugProtocol.VSSourceLinkInfo ToSourceLinkInfo(this SourceLink sourceLink) {
        return new DebugProtocol.VSSourceLinkInfo {
            Url = sourceLink?.Uri,
            RelativeFilePath = sourceLink?.RelativeFilePath,
        };
    }
    public static DebugProtocol.SetVariableResponse ToSetVariableResponse(this DebugProtocol.Variable variable) {
        return new DebugProtocol.SetVariableResponse {
            Value = variable.Value,
            Type = variable.Type,
            VariablesReference = variable.VariablesReference,
            NamedVariables = variable.NamedVariables,
            IndexedVariables = variable.IndexedVariables,
        };
    }
    public static DebugProtocol.GotoTargetsResponse ToJumpToCursorTarget(this DebugProtocol.GotoTargetsArguments args, int id) {
        return new DebugProtocol.GotoTargetsResponse {
            Targets = new List<DebugProtocol.GotoTarget>() {
            new DebugProtocol.GotoTarget {
                Id = id,
                Label = "Jump to cursor",
                Line = args.Line,
                Column = args.Column,
                EndLine = 0,
                EndColumn = 0
            }
        }
        };
    }
    public static DebugProtocol.ExceptionInfoResponse ToExceptionInfoResponse(this ExceptionInfo exeption) {
        return new DebugProtocol.ExceptionInfoResponse(exeption.Type, DebugProtocol.ExceptionBreakMode.Always) {
            Description = exeption.Message,
            Details = exeption.ToExceptionDetails(),
        };
    }
    private static DebugProtocol.ExceptionDetails? ToExceptionDetails(this ExceptionInfo? exception) {
        if (exception == null)
            return null;

        var innerExceptions = new List<ExceptionInfo>();
        if (exception.InnerException != null)
            innerExceptions.Add(exception.InnerException);

        var _innerExceptions = exception.InnerExceptions;
        if (_innerExceptions != null)
            innerExceptions.AddRange(_innerExceptions.Where(it => it != null));

        return new DebugProtocol.ExceptionDetails {
            FullTypeName = exception.Type,
            Message = exception.Message,
            InnerException = innerExceptions.Select(it => it.ToExceptionDetails()).ToList(),
            StackTrace = string.Join('\n', exception.StackTrace?.Select(it => it.ToStackTraceLine()) ?? Array.Empty<string>()),
        };
    }
    private static string ToStackTraceLine(this ExceptionStackFrame? frame) {
        var sb = new StringBuilder();
        if (frame?.DisplayText == null)
            return "<unknown>";

        sb.Append("    ");
        if (!frame.DisplayText.StartsWith("at "))
            sb.Append("at ");

        sb.Append(frame.DisplayText);
        if (!string.IsNullOrEmpty(frame.File))
            sb.Append($" in {frame.File}:line {frame.Line}");

        return sb.ToString();
    }
}
