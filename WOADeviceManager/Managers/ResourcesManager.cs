using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage;
using WOADeviceManager.Helpers;

namespace WOADeviceManager.Managers
{
    public class ResourcesManager
    {
        public enum DownloadableComponent
        {
            DRIVERS_VAYU,
            FD_VAYU,
            FD_SECUREBOOT_DISABLED_VAYU,
            PARTED,
            TWRP_VAYU,
            UEFI_VAYU,
            UEFI_SECUREBOOT_DISABLED_VAYU,
        }

        public static async Task<StorageFile> RetrieveFile(DownloadableComponent component, bool redownload = false)
        {
            string downloadPath = string.Empty;
            string fileName = string.Empty;
            string releaseVersion = string.Empty;

            switch (component)
            {
                case DownloadableComponent.PARTED:
                    downloadPath = "https://github.com/WOA-Project/SurfaceDuo-Guides/raw/main/Files/parted";
                    fileName = "parted";
                    break;

                case DownloadableComponent.TWRP_VAYU:
                    downloadPath = "https://github.com/woa-vayu/POCOX3Pro-Guides/releases/download/Recoveries/shrp-3.2_12-vayu.img";
                    fileName = "shrp-3.2_12-vayu.img";
                    break;
                case DownloadableComponent.UEFI_VAYU:
                    releaseVersion = await HttpsUtils.GetLatestBSPReleaseVersion();
                    downloadPath = $"https://github.com/woa-vayu/POCOX3Pro-Releases/releases/download/{releaseVersion}/POCO.X3.Pro.UEFI-v{releaseVersion}.img";
                    fileName = $"POCO.X3.Pro.UEFI-v{releaseVersion}.img";
                    break;
                case DownloadableComponent.UEFI_SECUREBOOT_DISABLED_VAYU:
                    releaseVersion = await HttpsUtils.GetLatestBSPReleaseVersion();
                    downloadPath = $"https://github.com/woa-vayu/POCOX3Pro-Releases/releases/download/{releaseVersion}/POCO.X3.Pro.UEFI-v{releaseVersion}.Secure.Boot.Disabled.img";
                    fileName = $"POCO.X3.Pro.UEFI-v{releaseVersion}.Secure.Boot.Disabled.img";
                    break;
                case DownloadableComponent.FD_VAYU:
                    releaseVersion = await HttpsUtils.GetLatestBSPReleaseVersion();
                    downloadPath = $"https://github.com/woa-vayu/POCOX3Pro-Releases/releases/download/{releaseVersion}/POCO.X3.Pro.UEFI-v{releaseVersion}.FD.for.making.your.own.Dual.Boot.Image.zip";
                    fileName = $"POCO.X3.Pro.UEFI-v{releaseVersion}.FD.for.making.your.own.Dual.Boot.Image.zip";
                    break;
                case DownloadableComponent.FD_SECUREBOOT_DISABLED_VAYU:
                    releaseVersion = await HttpsUtils.GetLatestBSPReleaseVersion();
                    downloadPath = $"https://github.com/woa-vayu/POCOX3Pro-Releases/releases/download/{releaseVersion}/POCO.X3.Pro.UEFI-v{releaseVersion}.Secure.Boot.Disabled.FD.for.making.your.own.Dual.Boot.Image.zip";
                    fileName = $"POCO.X3.Pro.UEFI-v{releaseVersion}.Secure.Boot.Disabled.FD.for.making.your.own.Dual.Boot.Image.zip";
                    break;
                case DownloadableComponent.DRIVERS_VAYU:
                    releaseVersion = await HttpsUtils.GetLatestBSPReleaseVersion();
                    downloadPath = $"https://github.com/woa-vayu/POCOX3Pro-Releases/releases/download/{releaseVersion}/POCOX3Pro-Drivers-v{releaseVersion}-Desktop.7z";
                    fileName = $"POCOX3Pro-Drivers-v{releaseVersion}-Desktop.7z";
                    break;
            }
            return await RetrieveFile(downloadPath, fileName, redownload);
        }

        public static async Task<StorageFile> RetrieveFile(string path, string fileName, bool redownload = false)
        {
            if (redownload || !IsFileAlreadyDownloaded(fileName))
            {
                StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                using HttpClient client = new();
                using Task<Stream> webStream = client.GetStreamAsync(new Uri(path));
                using FileStream fs = new(file.Path, FileMode.OpenOrCreate);
                webStream.Result.CopyTo(fs);
                return file;
            }
            else
            {
                return await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
            }
        }

        public static bool IsFileAlreadyDownloaded(string fileName)
        {
            return File.Exists(ApplicationData.Current.LocalFolder.Path + "\\" + fileName);
        }
    }
}
