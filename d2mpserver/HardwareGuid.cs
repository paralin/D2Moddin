// 
// HardwareGuid.cs
// Created by ilian000 on 2014-06-29
// Licenced under the Apache License, Version 2.0
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Management;
using System.Security.Cryptography;

namespace d2mpserver
{
    public class HardwareGuid
    {
        public static string getHardwareGuid()
        {
            string guid, drive = "";
            foreach (DriveInfo logicalDrive in DriveInfo.GetDrives())
            {
                if (logicalDrive.IsReady)
                {
                    drive = logicalDrive.RootDirectory.ToString();
                    break;
                }
            }
            if (drive.EndsWith(":\\"))
            {
                drive = drive.Substring(0, drive.Length - 2);
            }
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.Default.GetBytes(getDriveSerialnumber(drive) + getCpuIdentifier()));
                byte[] partHash = new byte[16];
                Array.Copy(hash, partHash, 16);
                guid = new Guid(partHash).ToString();
            }
            return guid;
        }
        private static string getDriveSerialnumber(string drive)
        {
            ManagementObject disk = new ManagementObject(@"win32_logicaldisk.deviceid=""" + drive + @":""");
            disk.Get();
            string volumeSerial = disk["VolumeSerialNumber"].ToString();
            disk.Dispose();
            return volumeSerial;
        }
        private static string getCpuIdentifier()
        {
            string cpuId = "";
            ManagementObjectCollection mCol = new ManagementClass("win32_processor").GetInstances();

            foreach (ManagementObject managObj in mCol)
            {
                cpuId = managObj.Properties["processorID"].Value.ToString();
            }
            return cpuId;
        }
    }
}
