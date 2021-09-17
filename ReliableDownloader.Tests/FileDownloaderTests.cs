using Moq;
using NUnit.Framework;
using System;

namespace ReliableDownloader.Tests
{
    [TestFixture]
    public class FileDownloaderTests
    {

        private string filePathUrl = "https://installerstaging.accurx.com/chain/3.55.11050.0/accuRx.Installer.Local.msi";
        private string localfilePath = @"C:/H/D drv/Pavitha/Coding/hello.txt";


        Mock<IWebSystemCalls> mockWebSystemCalls;
        Action<FileProgress> onProgressChanged;
        [SetUp]
        public void Setup()
        {
            mockWebSystemCalls = new Mock<IWebSystemCalls>();
            onProgressChanged = (x => Console.WriteLine($"Percent progress is {x.ProgressPercent}"));

        }

        [Test]
        public void Test1()
        {
            var fileDownloader = new FileDownloader();
            var res = fileDownloader.DownloadFile(filePathUrl, localfilePath, onProgressChanged);
            Assert.True(res.Result);
        }
    }
}