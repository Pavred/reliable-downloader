﻿using System;
using System.Threading.Tasks;
using ReliableDownloader;

namespace ReliableDownloader
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            // If this url 404's, you can get a live one from https://installerstaging.accurx.com/chain/latest.json.
            var exampleUrl = "https://installerstaging.accurx.com/chain/3.55.11050.0/accuRx.Installer.Local.msi";
            //var exampleFilePath = "C:/Users/[USER]/myfirstdownload.msi";

            var exampleFilePath = "C:/H/D drv/Pavitha/Coding/myfirstdownload.msi";
            var fileDownloader = new FileDownloader();
            await fileDownloader.DownloadFile(exampleUrl, exampleFilePath, progress => { Console.WriteLine($"Percent progress is {progress.ProgressPercent}% Estimate Time left is {progress.EstimatedRemaining} minutes "); });
        }
    }
}