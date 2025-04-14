﻿using HandheldCompanion.Utils;
using System;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace HandheldCompanion.Misc
{
    public class ProcessWindow : IDisposable
    {
        private AutomationPropertyChangedEventHandler handler;
        public event EventHandler Refreshed;
        public event EventHandler Closed;

        public AutomationElement Element { get; private set; }
        public readonly int Hwnd;
        private bool _disposed = false;

        private string _Name;
        public string Name
        {
            get => _Name;
            set
            {
                if (!value.Equals(_Name))
                {
                    _Name = value;
                    Refreshed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private AutomationEventHandler _windowClosedHandler;

        public ProcessWindow(AutomationElement element, bool isPrimary)
        {
            Hwnd = element.Current.NativeWindowHandle;
            Element = element;

            handler = new AutomationPropertyChangedEventHandler(OnPropertyChanged);
            if (element.TryGetCurrentPattern(WindowPattern.Pattern, out object patternObj))
            {
                Automation.AddAutomationPropertyChangedEventHandler(
                    Element,
                    TreeScope.Element,
                    handler,
                    AutomationElement.NameProperty,
                    AutomationElement.BoundingRectangleProperty);

                _windowClosedHandler = OnWindowClosed;
                Automation.AddAutomationEventHandler(
                    WindowPattern.WindowClosedEvent,
                    element,
                    TreeScope.Subtree,
                    _windowClosedHandler);
            }

            RefreshName();
        }

        private void OnWindowClosed(object sender, AutomationEventArgs e)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }

        ~ProcessWindow()
        {
            Dispose(false);
        }

        private void OnPropertyChanged(object sender, AutomationPropertyChangedEventArgs e)
        {
            try
            {
                if (Element != null)
                {
                    if (e.Property == AutomationElement.NameProperty)
                    {
                        RefreshName();
                    }
                    else if (e.Property == AutomationElement.BoundingRectangleProperty)
                    {
                        Refreshed?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch { }
        }

        public void RefreshName()
        {
            if (_disposed) return;

            try
            {
                string title = ProcessUtils.GetWindowTitle(Hwnd);
                if (!string.IsNullOrEmpty(title))
                    Name = title;
            }
            catch (COMException)
            {
                Dispose();
            }
            catch
            { }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try
                {
                    if (Element != null)
                    {
                        if (handler != null)
                            ProcessUtils.TaskWithTimeout(() =>
                                Automation.RemoveAutomationPropertyChangedEventHandler(Element, handler),
                                TimeSpan.FromSeconds(3));

                        if (_windowClosedHandler != null)
                            ProcessUtils.TaskWithTimeout(() =>
                                Automation.RemoveAutomationEventHandler(WindowPattern.WindowClosedEvent, Element, _windowClosedHandler),
                                TimeSpan.FromSeconds(3));
                    }
                }
                catch { }
            }

            // Clear references and mark as disposed
            Element = null;
            handler = null;
            _disposed = true;
        }
    }
}
