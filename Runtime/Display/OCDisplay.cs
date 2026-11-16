#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OperatorConsole
{
    /// <summary>The Display: the app's window on the screen. An Operator tries Display Settings, then confirms them within <see cref="ConfirmSeconds"/> or the previous Display comes back; only confirmed settings are saved.</summary>
    public sealed class OCDisplay
    {
        public const float ConfirmSeconds = 15f;

        private readonly Func<OCStore> _store;
        private OCDisplaySettings? _startup;
        private OCDisplaySettings? _previous;
        private OCDisplaySettings _trial;
        private CancellationTokenSource? _trialTimer;
        private float _deadline;

        internal OCDisplay(Func<OCStore> store) => _store = store;

        /// <summary>True when the Display can be changed: a Windows player. The tab is shown but inert elsewhere on Windows.</summary>
        public bool IsSupported => OCDisplayDriver.IsSupported;

        /// <summary>True when the Display tab belongs on this platform at all: Windows, in the Editor or a player.</summary>
        public static bool IsTabAvailable
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>True while a change is being applied.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>True while a tried change waits to be kept or reverted.</summary>
        public bool IsPending => _previous.HasValue;

        public float SecondsLeft => IsPending ? Mathf.Max(0f, _deadline - Time.unscaledTime) : 0f;

        public OCDisplaySettings Current => OCDisplayDriver.Capture();

        /// <summary>The Display the app started with, which Reset returns to.</summary>
        public OCDisplaySettings Startup => _startup ??= Current;

        public event Action? Changed;

        /// <summary>Applies <paramref name="settings"/> and starts the countdown to <see cref="Confirm"/>. Does nothing while busy or pending.</summary>
        public void Try(OCDisplaySettings settings)
        {
            if (!IsSupported || IsBusy || IsPending) return;
            TryAsync(settings).Forget();
        }

        /// <summary>Keeps the tried settings: saves them as Overrides.</summary>
        public void Confirm()
        {
            if (!IsPending) return;
            CancelTimer();
            _previous = null;
            var store = _store();
            OCDisplayStore.Write(store, _trial);
            store.Save();
            Changed?.Invoke();
        }

        /// <summary>Puts the Display back as it was before the tried settings.</summary>
        public void Revert()
        {
            if (!IsPending) return;
            RevertAsync().Forget();
        }

        /// <summary>Removes the saved Display Settings and restores the Display the app started with.</summary>
        public void Reset()
        {
            if (!IsSupported || IsBusy) return;
            CancelTimer();
            _previous = null;
            var store = _store();
            OCDisplayStore.Clear(store);
            store.Save();
            RestoreAsync(Startup).Forget();
        }

        internal void ApplyStoredAtStart()
        {
            if (!IsSupported || !OCDisplayStore.TryRead(_store(), out var saved)) return;
            _ = Startup;
            RestoreAsync(saved).Forget();
        }

        private async UniTaskVoid TryAsync(OCDisplaySettings settings)
        {
            var exit = Application.exitCancellationToken;
            _ = Startup;
            var previous = OCDisplayDriver.Capture();
            IsBusy = true;
            Changed?.Invoke();
            try
            {
                await OCDisplayDriver.ApplyAsync(settings, exit);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                await RestoreAsync(previous);
                return;
            }

            _trial = settings;
            _previous = previous;
            _deadline = Time.unscaledTime + ConfirmSeconds;
            IsBusy = false;
            using var timer = CancellationTokenSource.CreateLinkedTokenSource(exit);
            _trialTimer = timer;
            Changed?.Invoke();
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(ConfirmSeconds), DelayType.UnscaledDeltaTime, cancellationToken: timer.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            _trialTimer = null;
            await RevertAsync();
        }

        private async UniTask RevertAsync()
        {
            CancelTimer();
            var previous = _previous;
            _previous = null;
            if (previous.HasValue) await RestoreAsync(previous.Value);
            else Changed?.Invoke();
        }

        private async UniTask RestoreAsync(OCDisplaySettings settings)
        {
            IsBusy = true;
            Changed?.Invoke();
            try
            {
                await OCDisplayDriver.ApplyAsync(settings, Application.exitCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                IsBusy = false;
            }
            Changed?.Invoke();
        }

        private void CancelTimer()
        {
            var timer = _trialTimer;
            _trialTimer = null;
            timer?.Cancel();
        }
    }
}
