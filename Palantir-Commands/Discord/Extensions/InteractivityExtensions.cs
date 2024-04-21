using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;

namespace Palantir_Commands.Discord.Extensions;

public record InteractivityHandler<TInteractionResult>(Func<InteractivityExtension, Task<InteractivityResult<ComponentInteractionCreateEventArgs>>> InteractionListener, Func<InteractivityResult<ComponentInteractionCreateEventArgs>, Task<TInteractionResult>> InteractionHandler, TInteractionResult CancelledResult);

public static class InteractivityExtensions
{
    public static async Task<TInteractionResult> HandleNextInteraction<TInteractionResult>(this InteractivityExtension interactivity, IList<InteractivityHandler<TInteractionResult>> handlers)
    {
        var cancellationSources = handlers.ToDictionary(handler => handler, handler => new CancellationTokenSource());
        var cancellationLock = new object();

        var handlerAbstractions = handlers.Select<InteractivityHandler<TInteractionResult>, Func<Task<TInteractionResult>>>(handler => async () =>
        {
            // wait for interactivity to be triggered
            var result = await handler.InteractionListener.Invoke(interactivity);
            
            // check if it has been cancelled in the meantime
            if(cancellationSources[handler].IsCancellationRequested)
            {
                return handler.CancelledResult;
            }
            
            // else cancel others
            lock (cancellationLock)
            {
                foreach (var source in cancellationSources)
                {
                    if(source.Key != handler)
                    {
                        source.Value.Cancel();
                    }
                }
            }
            
            // return default if timed out
            if (result.TimedOut) return handler.CancelledResult;
            
            // handle interaction
            return await handler.InteractionHandler.Invoke(result);
        });
        
        var tasks = handlerAbstractions.Select(handler => Task.Run(handler.Invoke)).ToList();
        
        // wait for a task to finish and remove cancelled tasks
        Task<TInteractionResult> finishedTask;
        do
        {
            var candidates = tasks.Where(task => !task.IsCanceled).ToList();
            finishedTask = await Task.WhenAny(candidates);
        } while (finishedTask.IsCanceled);

        var result = await finishedTask;
        return result;
    }
}