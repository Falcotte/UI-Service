using System.Threading;
using System.Threading.Tasks;
using DG.Tweening;

namespace AngryKoala.Extensions
{
    public static class DOTweenExtensions
    {
        /// <summary>
        /// Awaits completion or kill of the tween.
        /// Optionally cancels and kills the tween if the provided CancellationToken is triggered.
        /// </summary>
        public static Task AwaitCompletionAsync(this Tween tween, CancellationToken cancellationToken = default)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            if (tween is not { active: true })
            {
                taskCompletionSource.SetResult(true);
                return taskCompletionSource.Task;
            }

            tween.onComplete += () =>
            {
                if (!taskCompletionSource.Task.IsCompleted)
                {
                    taskCompletionSource.SetResult(true);
                }
            };

            tween.onKill += () =>
            {
                if (!taskCompletionSource.Task.IsCompleted)
                {
                    taskCompletionSource.SetResult(true);
                }
            };

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    if (tween.IsActive())
                    {
                        tween.Kill(false);
                    }

                    if (!taskCompletionSource.Task.IsCompleted)
                    {
                        taskCompletionSource.SetCanceled();
                    }
                });
            }

            return taskCompletionSource.Task;
        }
    }
}