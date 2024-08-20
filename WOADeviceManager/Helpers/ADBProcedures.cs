﻿using System;
using System.Threading.Tasks;
using WOADeviceManager.Managers;

namespace WOADeviceManager.Helpers
{
    public class ADBProcedures
    {
        public static string GetDeviceProductModel()
        {
            return DeviceManager.Device.AndroidDebugBridgeTransport.GetVariableValue("ro.product.model");
        }

        public static string GetDeviceProductDevice()
        {
            return DeviceManager.Device.AndroidDebugBridgeTransport.GetVariableValue("ro.product.device");
        }

        public static string GetDeviceProductName()
        {
            return DeviceManager.Device.AndroidDebugBridgeTransport.GetVariableValue("ro.product.name");
        }

        public static string GetDeviceBuildVersionRelease()
        {
            string? result = DeviceManager.Device.AndroidDebugBridgeTransport.GetVariableValue("ro.build.version.release");
            return result == null ? null : $"Android {result}";
        }

        public static string GetDeviceBuildId()
        {
            return DeviceManager.Device.AndroidDebugBridgeTransport.GetVariableValue("ro.build.id");
        }

        public static string GetDeviceWallpaperPath()
        {
            throw new NotImplementedException();
        }

        public static void RebootToBootloader()
        {
            DeviceManager.Device.AndroidDebugBridgeTransport.RebootBootloader();
        }

        public static void RebootToAndroid()
        {
            DeviceManager.Device.AndroidDebugBridgeTransport.Reboot();
        }

        public static void RebootToFastBootD()
        {
            DeviceManager.Device.AndroidDebugBridgeTransport.RebootFastBootD();
        }

        public static void RebootToRecovery()
        {
            DeviceManager.Device.AndroidDebugBridgeTransport.RebootRecovery();
        }

        public static bool IsUnlocked()
        {
            return DeviceManager.Device.AndroidDebugBridgeTransport.GetVariableValue("ro.boot.flash.locked") == "0";
        }

        public static bool CanUnlock()
        {
            return DeviceManager.Device.AndroidDebugBridgeTransport.GetVariableValue("sys.oem_unlock_allowed") == "1";
        }

        public static async Task<bool> PushParted()
        {
            /*StorageFile parted = await ResourcesManager.RetrieveFile(ResourcesManager.DownloadableComponent.PARTED);
            Progress<int> progress = new();
            bool completed = false;
            progress.ProgressChanged += (object sender, int e) =>
            {
                completed = e == 100;
            };
            try
            {
                using (SyncService service = new(ADBManager.Client, DeviceManager.GetADBDeviceDataFromUSBID(DeviceManager.Device.ID)))
                using (Stream stream = File.OpenRead(@parted.Path))
                {
                    service.Push(stream, "/sdcard/parted", 755, DateTime.Now, progress, CancellationToken.None);
                }
                while (!completed)
                {
                    await Task.Delay(500);
                }

                return true;
            }
            // TODO: Handle exception (file transfer failed)
            catch { }*/
            return false;
        }

        public static string GetDeviceBatteryLevel()
        {
            string result = null;
            try
            {
                result = DeviceManager.Device.AndroidDebugBridgeTransport?.Shell("dumpsys battery") ?? "";
                if (result.Contains("level: "))
                {
                    result = result.Split("level: ")[1].Split("\n")[0].Trim();
                }
                else
                {
                    result = null;
                }
                return result;
            }
            catch (Exception) { }
            return result;
        }

        public static async Task EnableMassStorageMode()
        {
            string MassStorageOneLiner = @"msc";

            try
            {
                DeviceManager.Device.AndroidDebugBridgeTransport.Shell(MassStorageOneLiner);
            }
            catch (Exception) { }

            await Task.Delay(200);
        }
    }
}

/*

Valuable info in getprop

[ro.build.id]: [2022.823.34]
[ro.vendor.build.security_patch]: [2023-02-01]
[ro.product.model]: [Surface Duo]
[ro.build.version.incremental]: [202208230034]
[ro.build.version.release]: [12]

[sys.boot.reason]: [shutdown,battery]
[sys.boot.reason.last]: [shutdown,battery]

*/