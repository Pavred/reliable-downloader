using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ReliableDownloader
{
    public class FileDownloader : IFileDownloader
    {
        private static readonly WebSystemCalls _client = new WebSystemCalls();
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private readonly long _batchSize = 1024;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contentFileUrl"></param>
        /// <param name="localFilePath"></param>
        /// <param name="onProgressChanged"></param>
        /// <returns></returns>
        public Task<bool> DownloadFile(string contentFileUrl, string localFilePath, Action<FileProgress> onProgressChanged)
        {
            try
            {
                return Task.FromResult(DownloadAsync(contentFileUrl, localFilePath, onProgressChanged).Result);

            }

            catch (Exception ex)
            {
                throw ex;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        public void CancelDownloads()
        {
            _cancellationTokenSource?.Cancel(false);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="contentFileUrl"></param>
        /// <param name="localFilePath"></param>
        /// <param name="onProgressChanged"></param>
        /// <returns></returns>
        private async Task<bool> DownloadAsync(string contentFileUrl, string localFilePath, Action<FileProgress> onProgressChanged)
        {

            var header = await _client.GetHeadersAsync(contentFileUrl, _cancellationTokenSource.Token).ConfigureAwait(false);

            using (var response = DownloadContents(contentFileUrl, localFilePath, header))
            {
                await ProcessResponseToFile(response.Result, localFilePath, onProgressChanged);


            }
            return CheckFileIntegrity(localFilePath, header);
        }


        /// <summary>
        /// Download contents either partially or full
        /// </summary>
        /// <param name="contentFileUrl"></param>
        /// <param name="localFilePath"></param>
        /// <param name="header"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> DownloadContents(string contentFileUrl, string localFilePath, HttpResponseMessage header)
        {
            HttpResponseMessage responseMessage;

            long ExistingLength = 0;

            if (File.Exists(localFilePath))
            {
                FileInfo fileInfo = new FileInfo(localFilePath);
                ExistingLength = fileInfo.Length;
            }

            try
            {
                if (header.Headers.AcceptRanges != null && header.Headers.AcceptRanges.Contains("bytes"))
                {

                    responseMessage = await _client.DownloadPartialContent(contentFileUrl, ExistingLength, ExistingLength + _batchSize, _cancellationTokenSource.Token).ConfigureAwait(false);
                }
                else
                {
                    responseMessage = await _client.DownloadContent(contentFileUrl, _cancellationTokenSource.Token).ConfigureAwait(false);

                }
                return responseMessage;
            }
            catch (TaskCanceledException) // when stream reader timeout occurred 
            {
                // re-request and continue downloading...
                responseMessage = await _client.DownloadContent(contentFileUrl, _cancellationTokenSource.Token).ConfigureAwait(false);
                return responseMessage;
            }
            catch (Exception error) when (error.Message == ("System.Net.Http") || error.Message == ("System.Net.Sockets") ||
                                           error.Message == ("System.Net.Security") || error.Message == "No such host is known.")
            {
                await Task.Delay(5000, _cancellationTokenSource.Token).ConfigureAwait(false);
                // re-request and continue downloading...
                responseMessage = await _client.DownloadContent(contentFileUrl, _cancellationTokenSource.Token).ConfigureAwait(false);
                return responseMessage;
            }
            finally
            {
                await Task.Yield();
            }
        }

        /// <summary>
        /// Write downloaded contents to file
        /// </summary>
        /// <param name="response"></param>
        /// <param name="localFilePath"></param>
        /// <param name="onProgressChanged"></param>
        /// <returns></returns>
        private async Task ProcessResponseToFile(HttpResponseMessage response, string localFilePath, Action<FileProgress> onProgressChanged)
        {


            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;

            using (var contentStream = await response.Content.ReadAsStreamAsync())
                await ProcessContentStream(totalBytes, contentStream, localFilePath, onProgressChanged);
        }

        /// <summary>
        /// Start writing to a file
        /// </summary>
        /// <param name="totalDownloadSize"></param>
        /// <param name="contentStream"></param>
        /// <param name="localFilePath"></param>
        /// <param name="onProgressChanged"></param>
        /// <returns></returns>
        private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream, string localFilePath, Action<FileProgress> onProgressChanged)
        {
            var totalBytesRead = 0L;
            var readCount = 0L;
            var buffer = new byte[_batchSize];
            var isMoreToRead = true;

            using (Stream fileStream = File.OpenWrite(localFilePath))
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                do
                {
                    var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, bytesRead);

                    totalBytesRead += bytesRead;
                    readCount += 1;
                    TriggerProgressChanged(totalDownloadSize, totalBytesRead, stopwatch.ElapsedMilliseconds, onProgressChanged);

                }
                while (isMoreToRead);
            }
        }

        /// <summary>
        /// Logs the progress
        /// </summary>
        /// <param name="totalDownloadSize"></param>
        /// <param name="totalBytesRead"></param>
        /// <param name="elapsedTime"></param>
        /// <param name="onProgressChanged"></param>

        private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead, long elapsedTime, Action<FileProgress> onProgressChanged)
        {
            // (long? totalFileSize, long totalBytesDownloaded, double? progressPercent, TimeSpan? estimatedRemaining)
            if (onProgressChanged == null)
                return;

            double? progressPercentage = null;
            if (totalDownloadSize.HasValue)
                progressPercentage = Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2);

            int estimatedRemainingTime = (int)Math.Ceiling((double)(elapsedTime * totalDownloadSize) / (totalBytesRead) - elapsedTime);

            onProgressChanged(new FileProgress(totalDownloadSize, totalBytesRead, progressPercentage, new TimeSpan(0, 0, estimatedRemainingTime, 0, 0)));
        }


        /// <summary>
        /// check for file integrity after the file is downloaded
        /// </summary>
        /// <param name="localFilePath"></param>
        /// <param name="headerResponse"></param>
        /// <returns></returns>
        private bool CheckFileIntegrity(string localFilePath, HttpResponseMessage headerResponse)
        {
            bool result = false;
            var contentMD5 = headerResponse.Content.Headers.ContentMD5;
            if (contentMD5 != null)
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(localFilePath))
                    {
                        var bytesFoeMD5 = md5.ComputeHash(stream);
                        if (contentMD5.SequenceEqual(bytesFoeMD5))
                            result = true;
                    }
                }
            }

            return result;
        }
    }
}