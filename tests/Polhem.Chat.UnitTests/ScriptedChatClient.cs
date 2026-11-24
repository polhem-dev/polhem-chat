using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Polhem.Chat.UnitTests;

/// <summary>
/// An <see cref="IChatClient"/> that replays a script instead of calling a model: each step yields an update, waits, runs
/// an action or throws. It records the messages of every request.
/// </summary>
internal sealed class ScriptedChatClient(params ScriptedChatClient.Step[] steps) : IChatClient
{
    public abstract record Step;

    public sealed record Yield(ChatResponseUpdate Update) : Step;

    /// <summary>Waits until <see cref="Release"/> is called or the request is canceled.</summary>
    public sealed record Pause : Step
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed record Run(Action Action) : Step;

    public sealed record Throw(Exception Exception) : Step;

    public List<List<ChatMessage>> Requests { get; } = [];

    public static Yield Text(string text) => new(new ChatResponseUpdate(ChatRole.Assistant, text));

    public static Yield Contents(params AIContent[] contents) => new(new ChatResponseUpdate(ChatRole.Assistant, [.. contents]));

    public static Yield ToolContents(params AIContent[] contents) => new(new ChatResponseUpdate(ChatRole.Tool, [.. contents]));

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The runner streams.");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Requests.Add([.. messages]);
        foreach (var step in steps)
        {
            switch (step)
            {
                case Yield yield:
                    yield return yield.Update;
                    break;
                case Pause pause:
                    pause.Reached.TrySetResult();
                    await pause.Release.Task.WaitAsync(cancellationToken);
                    break;
                case Run run:
                    run.Action();
                    break;
                case Throw @throw:
                    throw @throw.Exception;
            }
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
