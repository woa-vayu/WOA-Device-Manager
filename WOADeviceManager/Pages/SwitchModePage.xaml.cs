﻿using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WOADeviceManager.Helpers;
using WOADeviceManager.Managers;
using WOADeviceManager.Managers.Connectivity;

namespace WOADeviceManager.Pages
{
    public sealed partial class SwitchModePage : Page
    {
        public SwitchModePage()
        {
            InitializeComponent();
        }

        private void RebootToAndroid_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (o) =>
            {
                MainPage.SetStatus("Rebooting phone to Android mode...", Emoji: "🔄️");

                try
                {
                    await DeviceRebootHelper.RebootToAndroidAndWait();
                }
                catch (Exception ex)
                {
                    MainPage.ShowDialog(ex.Message);
                }

                MainPage.ToggleLoadingScreen(false);
            });
        }

        private async void RebootToBootloader_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (o) =>
            {
                MainPage.SetStatus("Rebooting phone to Bootloader mode...", Emoji: "🔄️");

                try
                {
                    await DeviceRebootHelper.RebootToBootloaderAndWait();
                }
                catch (Exception ex)
                {
                    MainPage.ShowDialog(ex.Message);
                }

                MainPage.ToggleLoadingScreen(false);
            });
        }

        private async void RebootToFastBootd_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (o) =>
            {
                MainPage.SetStatus("Rebooting phone to FastBootd mode...", Emoji: "🔄️");

                try
                {
                    await DeviceRebootHelper.RebootToFastBootDAndWait();
                }
                catch (Exception ex)
                {
                    MainPage.ShowDialog(ex.Message);
                }

                MainPage.ToggleLoadingScreen(false);
            });
        }

        private async void RebootToMassStorageMode_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (o) =>
            {
                MainPage.SetStatus("Rebooting phone to Mass Storage mode...", Emoji: "🔄️");

                try
                {
                    await DeviceRebootHelper.RebootToMSCAndWait();
                }
                catch { }

                MainPage.ToggleLoadingScreen(false);
            });
        }

        private async void RebootToRecovery_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (o) =>
            {
                MainPage.SetStatus("Rebooting phone to Recovery mode...", Emoji: "🔄️");

                try
                {
                    await DeviceRebootHelper.RebootToRecoveryAndWait();
                }
                catch (Exception ex)
                {
                    MainPage.ShowDialog(ex.Message);
                }

                MainPage.ToggleLoadingScreen(false);
            });
        }

        private async void RebootToTWRP_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (o) =>
            {
                MainPage.SetStatus("Rebooting phone to TWRP mode...", Emoji: "🔄️");

                try
                {
                    await DeviceRebootHelper.RebootToTWRPAndWait();
                }
                catch (Exception ex)
                {
                    MainPage.ShowDialog(ex.Message);
                }

                MainPage.ToggleLoadingScreen(false);
            });
        }

        private async void RebootToWindows_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (o) =>
            {
                MainPage.SetStatus("Rebooting phone to Windows mode...", Emoji: "🔄️");

                try
                {
                    await DeviceRebootHelper.RebootToUEFI();
                }
                catch (Exception ex)
                {
                    MainPage.ShowDialog(ex.Message);
                }

                MainPage.ToggleLoadingScreen(false);
            });
        }

        private async void RebootToWindows_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (o) =>
            {
                MainPage.SetStatus("Rebooting phone to Windows mode...", Emoji: "🔄️");

                string? UEFIFile = null;

                FileOpenPicker picker = new()
                {
                    ViewMode = PickerViewMode.List,
                    SuggestedStartLocation = PickerLocationId.Downloads,
                    FileTypeFilter = { ".img" }
                };

                nint windowHandle = WindowNative.GetWindowHandle(App.mainWindow);
                InitializeWithWindow.Initialize(picker, windowHandle);

                Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
                if (file != null && File.Exists(file.Path))
                {
                    UEFIFile = file.Path;
                }
                else
                {
                    MainPage.ToggleLoadingScreen(false);
                    return;
                }

                try
                {
                    await DeviceRebootHelper.RebootToUEFI(UEFIFile);
                }
                catch (Exception ex)
                {
                    MainPage.ShowDialog(ex.Message);
                }

                MainPage.ToggleLoadingScreen(false);
            });
        }

        private void Shutdown_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (o) =>
            {
                MainPage.SetStatus("Shutting Down Phone (UFP)...", SubMessage: "Please disconnect your phone now in order to shut it down!", Emoji: "🔄️");

                try
                {
                    UFPProcedures.Shutdown();
                }
                catch (Exception ex)
                {
                    MainPage.ShowDialog(ex.Message);
                }

                MainPage.ToggleLoadingScreen(false);
            });
        }

        private void Reboot_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (o) =>
            {
                MainPage.SetStatus("Rebooting phone (UFP)...", Emoji: "🔄️");
                try
                {
                    UFPProcedures.Reboot();
                }
                catch (Exception ex)
                {
                    MainPage.ShowDialog(ex.Message);
                }
                MainPage.ToggleLoadingScreen(false);
            });
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (o) =>
            {
                MainPage.SetStatus("Continuing Boot (UFP)...", Emoji: "🔄️");
                try
                {
                    UFPProcedures.ContinueBoot();
                }
                catch (Exception ex)
                {
                    MainPage.ShowDialog(ex.Message);
                }
                MainPage.ToggleLoadingScreen(false);
            });
        }

        private void MassStorage_Click(object sender, RoutedEventArgs e)
        {
            MainPage.SetStatus("Rebooting phone to Mass Storage mode (UFP)...", Emoji: "🔄️");
            try
            {
                UFPProcedures.MassStorage();
            }
            catch { }
            MainPage.ToggleLoadingScreen(false);
        });
        }

    private void Instance_DeviceDisconnectedEvent(object sender, Device device)
    {
        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            Bindings.Update();
        });
    }

    private void DeviceManager_DeviceConnectedEvent(object sender, Device device)
    {
        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            Bindings.Update();
        });
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        DeviceManager.DeviceConnectedEvent += DeviceManager_DeviceConnectedEvent;
        DeviceManager.DeviceDisconnectedEvent += Instance_DeviceDisconnectedEvent;
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        DeviceManager.DeviceConnectedEvent -= DeviceManager_DeviceConnectedEvent;
        DeviceManager.DeviceDisconnectedEvent -= Instance_DeviceDisconnectedEvent;
    }
}