﻿using HandheldCompanion.Shared;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Notifications;

namespace HandheldCompanion.Managers
{
    public static class ToastManager
    {
        private const int Interval = 1000; // ms
        private const string Group = "HandheldCompanion";

        private static readonly ConcurrentQueue<(string Title, string Content, string Img, bool IsHero)> ToastQueue = new();
        private static readonly SemaphoreSlim QueueSemaphore = new(1, 1);
        private static DateTime LastToastTime = DateTime.MinValue;

        private static ToastNotification CurrentToastNotification;

        public static bool IsEnabled => ManagerFactory.settingsManager.GetBoolean("ToastEnable");
        private static bool IsInitialized { get; set; }

        static ToastManager() { }

        public static bool SendToast(string title, string content = "", string img = "icon", bool isHero = false)
        {
            if (!IsEnabled)
                return false;

            ToastQueue.Enqueue((title, content, img, isHero));
            _ = ProcessToastQueue();

            return true;
        }

        private static async Task ProcessToastQueue()
        {
            if (!QueueSemaphore.Wait(1000)) return; // Prevent concurrent processing

            try
            {
                while (ToastQueue.TryDequeue(out var toastData))
                {
                    TimeSpan timeSinceLastToast = DateTime.Now - LastToastTime;
                    if (timeSinceLastToast.TotalMilliseconds < Interval)
                        await Task.Delay(Interval - (int)timeSinceLastToast.TotalMilliseconds);

                    DisplayToast(toastData.Title, toastData.Content, toastData.Img, toastData.IsHero);
                    LastToastTime = DateTime.Now;
                }
            }
            catch { }
            finally
            {
                QueueSemaphore.Release();
            }
        }

        private static void DisplayToast(string title, string content, string img, bool isHero)
        {
            string imagePath = $"{AppDomain.CurrentDomain.BaseDirectory}Resources\\{img}.png";
            Uri imageUri = new Uri($"file:///{imagePath}");

            ToastContentBuilder toast = new ToastContentBuilder()
                .AddText(title)
                .AddText(content)
                .AddAudio(new ToastAudio { Silent = true, Src = new Uri("ms-winsoundevent:Notification.Default") })
                .SetToastScenario(ToastScenario.Default);

            if (File.Exists(imagePath))
            {
                if (isHero)
                    toast.AddHeroImage(imageUri);
                else
                    toast.AddAppLogoOverride(imageUri, ToastGenericAppLogoCrop.Default);
            }

            toast.Show(toastNotification =>
            {
                toastNotification.Tag = title;
                toastNotification.Group = Group;

                // Set the expiration time (affects Action Center, not on-screen duration)
                // toastNotification.ExpirationTime = DateTimeOffset.Now.AddMilliseconds(Interval);

                // Attach event handlers
                toastNotification.Dismissed += (sender, args) =>
                {
                    if (args.Reason == ToastDismissalReason.UserCanceled ||
                        args.Reason == ToastDismissalReason.TimedOut)
                    {
                        TriggerNextToast();
                    }
                };

                /*
                // Manually remove the toast from the Action Center after the interval
                _ = Task.Run(async () =>
                {
                    await Task.Delay(Interval);
                    ToastNotificationManagerCompat.History.Remove(title, Group);
                });
                */
            });
        }

        private static void TriggerNextToast()
        {
            // Immediately process the next toast in the queue
            _ = ProcessToastQueue();
        }

        public static void Start()
        {
            if (IsInitialized)
                return;

            IsInitialized = true;
            LogManager.LogInformation("{0} has started", nameof(ToastManager));
        }

        public static void Stop()
        {
            if (!IsInitialized)
                return;

            ToastQueue.Clear();

            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", nameof(ToastManager));
        }
    }
}