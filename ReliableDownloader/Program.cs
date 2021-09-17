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
            //https://newbedev.com/progress-bar-with-httpclient
            //  var exampleFilePath = "C:/Users/[USER]/myfirstdownload.msi";
          //  var exampleUrl = "http://deelay.me/500/https://en.wikipedia.org/wiki/Text_file#:~:text=A%20text%20file%20(sometimes%20spelled,within%20a%20computer%20file%20system.&text=%22Text%20file%22%20refers%20to%20a,to%20a%20type%20of%20content.";

             var exampleFilePath = "C:/H/D drv/Pavitha/Coding/myfirstdownload.msi";
            var fileDownloader = new FileDownloader();
            await fileDownloader.DownloadFile(exampleUrl, exampleFilePath, progress => { Console.WriteLine($"Percent progress is {progress.ProgressPercent}% Estimate Time left is {progress.EstimatedRemaining} minutes "); });
        }
    }
}