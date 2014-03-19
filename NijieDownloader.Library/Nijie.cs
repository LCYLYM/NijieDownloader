﻿using System;
using System.IO;
using System.Net;
using System.Text;
using HtmlAgilityPack;
using log4net;
using Nandaka.Common;
using System.Threading;

namespace NijieDownloader.Library
{
    public partial class Nijie
    {
        private ILog _log;
        public ILog Log
        {
            get
            {
                if (_log == null)
                {
                    log4net.GlobalContext.Properties["Date"] = DateTime.Now.ToString("yyyy-MM-dd");
                    _log = LogManager.GetLogger(typeof(Nijie));
                    log4net.Config.XmlConfigurator.Configure();
                    _log.Logger.Repository.Threshold = log4net.Core.Level.All;
                }
                return _log;
            }
            private set
            {
                _log = value;
            }
        }

        public bool UseHttps { get; set; }

        public Nijie(ILog _log, bool useHttps)
        {
            this.Log = _log;
            ExtendedWebClient.EnableCompression = true;
            ExtendedWebClient.EnableCookie = true;
            var proxy = ExtendedWebClient.GlobalProxy;
            Log.Debug("Proxy= " + proxy);
            UseHttps = useHttps;
            Log.Debug("UseHttps= " + useHttps);
        }

        private void canOperate()
        {
            if (!IsLoggedIn)
            {
                throw new NijieException("Not Logged In", NijieException.NOT_LOGGED_IN);
            }
        }

        private Tuple<HtmlDocument, WebResponse> getPage(string url)
        {
            ExtendedWebClient client = new ExtendedWebClient();
            var imagePage = client.DownloadData(url);

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(Encoding.UTF8.GetString(imagePage));
            return new Tuple<HtmlDocument, WebResponse>(doc, client.Response);
        }

        public string Download(string url, string referer, string filename, bool overwrite, bool overwriteOnlyIfDifferentSize, bool makeBackup, Action<string> progressChanged)
        {
            String message = "";
            var fileExist = File.Exists(filename);
            if (fileExist)
            {
                message = "File Exists: " + filename;
                // skip download if overwrite flag is not tick
                if (!overwrite)
                {
                    message += ", skipping...";
                    Log.Warn(message);
                    if(progressChanged != null) 
                        progressChanged(message);
                    return message;
                }
            }

            ExtendedWebClient client = new ExtendedWebClient();
            client.Referer = referer;
            var tempFilename = filename + ".!nijie";
            if (File.Exists(tempFilename))
            {
                var msg2 = "Deleting temporary file: " + tempFilename;
                Log.Debug(msg2);
                if (progressChanged != null)
                    progressChanged(msg2);
                File.Delete(tempFilename);
            }

            Util.CreateSubDir(tempFilename);
            try
            {
                using (var stream = client.OpenRead(url))
                {
                    if (fileExist)
                    {
                        FileInfo oldFileInfo = new FileInfo(filename);
                        Int64 bytes_total = -1;
                        if (client.ResponseHeaders["Content-Length"] != null)
                            bytes_total = Convert.ToInt64(client.ResponseHeaders["Content-Length"]);

                        Log.Debug("Filesize: " + bytes_total);

                        if (bytes_total != -1 && oldFileInfo.Length == bytes_total)
                        {
                            // skip download if the filesize are the same.
                            if (overwriteOnlyIfDifferentSize)
                            {
                                message += ", Identical size: " + bytes_total + ", skipping...";
                                Log.Warn(message);
                                if (progressChanged != null)
                                    progressChanged(message);
                                return message;
                            }
                        }
                        else
                        {
                            // make backup for the old file
                            if (makeBackup)
                            {
                                var backupFilename = filename + "." + Util.DateTimeToUnixTimestamp(DateTime.Now);
                                message += ", different size: " + oldFileInfo.Length + " vs " + bytes_total + ", backing up to: " + backupFilename;
                                Log.Info(message);
                                if (progressChanged != null)
                                    progressChanged(message);
                                File.Move(filename, backupFilename);
                            }
                        }
                    }
                    using (var f = File.Create(tempFilename))
                    {
                        stream.CopyTo(f);
                    }
                }
                //client.DownloadFile(url, tempFilename);
            }
            catch (Exception ex)
            {
                throw new NijieException(string.Format("Error when downloading: {0} to {1}", url, tempFilename), ex, NijieException.DOWNLOAD_ERROR);
            }
            Thread.Sleep(100); // delay before renaming
            File.Move(tempFilename, filename);
            message = "Saved to: " + filename;
            if (progressChanged != null)
                progressChanged(message);
            return message;
        }

        public byte[] DownloadData(string url, string referer)
        {
            try
            {
                ExtendedWebClient client = new ExtendedWebClient();
                client.Referer = referer;
                return client.DownloadData(url);
            }
            catch (Exception ex)
            {
                throw new NijieException("Error when downloading data: " + url, ex, NijieException.DOWNLOAD_ERROR); ;
            }
        }
    }
}
