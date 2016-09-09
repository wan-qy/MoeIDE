﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Meowtrix.WPF.Extend;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text.Editor;

namespace Meowtrix.MoeIDE
{
    /// <summary>
    /// Adornment class that draws a square box in the top right hand corner of the viewport
    /// </summary>
    public sealed class EditorBackground
    {
        /// <summary>
        /// Text view to add the adornment on.
        /// </summary>
        private readonly IWpfTextView view;

        private ContentControl control;
        private Grid parentGrid;
        private Canvas viewStack;
        private Grid leftMargin;

        private RECT hostRect;
        private Panel hostRootVisual;
        private VisualBrush hostVisualBrush;

        /// <summary>
        /// Initializes a new instance of the <see cref="EditorBackground"/> class.
        /// </summary>
        /// <param name="view">The <see cref="IWpfTextView"/> upon which the adornment will be drawn</param>
        public EditorBackground(IWpfTextView view)
        {
            this.view = view;
            control = (ContentControl)view;
            VSColorTheme.ThemeChanged += _ => control.Dispatcher.Invoke(MakeBackgroundTransparent, DispatcherPriority.Render);
            control.Loaded += TextView_Loaded;
        }

        private void TextView_Loaded(object sender, RoutedEventArgs e)
        {
            if (parentGrid == null) parentGrid = control.Parent as Grid;
            if (viewStack == null) viewStack = control.Content as Canvas;
            if (leftMargin == null) leftMargin = (parentGrid.BFS().FirstOrDefault(x => x.GetType().Name == "LeftMargin") as Grid)?.Children[0] as Grid;

            if (!control.IsDescendantOf(Application.Current.MainWindow))
            {
                var source = PresentationSource.FromVisual(control) as HwndSource;
                hostRootVisual = source.RootVisual as Panel;
                if (hostRootVisual?.GetType().Name == "WpfMultiViewHost")//xaml editor
                {
                    source.AddHook(WndHook);
                    var mainWindow = Application.Current.MainWindow;
                    hostVisualBrush = new VisualBrush(((Grid)mainWindow.Template.FindName("RootGrid", mainWindow)).Children[0]);
                    hostRootVisual.Background = hostVisualBrush;
                    WeakEventManager<Window, SizeChangedEventArgs>
                        .AddHandler(mainWindow, nameof(Window.SizeChanged), (_, __) => SetVisualBrush());
                }
            }

            MakeBackgroundTransparent();
        }

        private IntPtr WndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            RECT rect;
            NativeMethods.GetWindowRect(hwnd, out rect);
            if (hostRect.Left != rect.Left ||
                hostRect.Right != rect.Right ||
                hostRect.Top != rect.Top ||
                hostRect.Bottom != rect.Bottom)
            {
                hostRect = rect;
                SetVisualBrush();
            }
            return IntPtr.Zero;
        }

        private void SetVisualBrush()
        {
            RECT mainRect;
            NativeMethods.GetWindowRect(((HwndSource)PresentationSource.FromVisual(Application.Current.MainWindow)).Handle,
                out mainRect);
            double x = (hostRect.Left - mainRect.Left) / (double)mainRect.Width,
                y = (hostRect.Top - mainRect.Top) / (double)mainRect.Height,
                width = hostRect.Width / (double)mainRect.Width,
                height = hostRect.Height / (double)mainRect.Height;
            if (x < 0 || y < 0 || width > 1 || height > 1) return;
            hostVisualBrush.Viewbox = new Rect(x, y, width, height);
        }

        private void MakeBackgroundTransparent()
        {
            if (parentGrid != null) parentGrid.ClearValue(Panel.BackgroundProperty);
            if (viewStack != null) viewStack.Background = new VisualBrush(parentGrid);
            if (leftMargin != null) leftMargin.Background = Brushes.Transparent;
        }
    }
}
