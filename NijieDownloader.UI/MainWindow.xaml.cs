﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using FirstFloor.ModernUI.Windows.Controls;

using NijieDownloader.Library;
using Nandaka.Common;
using System.Threading.Tasks;
using System.IO;
using NijieDownloader.UI.ViewModel;
using NijieDownloader.Library.Model;
using System.Runtime.Caching;
using System.Diagnostics;
using System.Collections.Specialized;
using System.Threading;

namespace NijieDownloader.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : ModernWindow
    {
        public static Nijie Bot { get; private set; }
        public static LimitedConcurrencyLevelTaskScheduler lcts = new LimitedConcurrencyLevelTaskScheduler(5);
        public static LimitedConcurrencyLevelTaskScheduler lctsJob = new LimitedConcurrencyLevelTaskScheduler(2);
        public static TaskFactory Factory { get; private set; }
        public static TaskFactory JobFactory { get; private set; }

        public const string IMAGE_LOADING = "Loading";
        public const string IMAGE_LOADED = "Done";
        public const string IMAGE_ERROR = "Error";

        private static ObjectCache cache;

        public MainWindow()
        {
            InitializeComponent();
            Bot = new Nijie();
            Nijie.LoggingEventHandler += new Nijie.NijieEventHandler(Nijie_LoggingEventHandler);
            Factory = new TaskFactory(lcts);
            JobFactory = new TaskFactory(lctsJob);

            var config = new NameValueCollection();
            config.Add("pollingInterval", "00:05:00");
            config.Add("physicalMemoryLimitPercentage", "0");
            config.Add("cacheMemoryLimitMegabytes", "100");
            cache = new MemoryCache("CustomCache", config);
        }

        void Nijie_LoggingEventHandler(object sender, bool e)
        {
            if (e)
            {
                tlLogin.DisplayName = "Logout";
            }
            else
            {
                tlLogin.DisplayName = "Login";
            }
        }

        public static void LoadImage(string url, string referer, Action<BitmapImage, string> action)
        {
            if (String.IsNullOrWhiteSpace(url)) return;
            url = Util.FixUrl(url);
            referer = Util.FixUrl(referer);
            if (!cache.Contains(url))
            {

                Factory.StartNew(() =>
                {
                    try
                    {
                        var result = MainWindow.Bot.DownloadData(url, referer);
                        using (var ms = new MemoryStream(result))
                        {
                            var t = new BitmapImage();
                            t.BeginInit();
                            t.CacheOption = BitmapCacheOption.OnLoad;
                            t.StreamSource = ms;
                            t.EndInit();
                            t.Freeze();
                            action(t, IMAGE_LOADED);

                            CacheItemPolicy policy = new CacheItemPolicy();
                            policy.SlidingExpiration = new TimeSpan(1, 0, 0);
                            //cache.Add(url, t, policy);
                            cache.Set(url, t, policy);
                        }
                    }
                    catch (Exception ex)
                    {
                        action(null, IMAGE_ERROR);
                        Debug.WriteLine("Error when loading image: {0}", ex.Message);
                    }
                }
                );
            }
            else
            {
                action((BitmapImage)cache.Get(url), IMAGE_LOADED);
            }
        }

        public static void DoJob(JobDownloadViewModel job)
        {
            job.Status = Status.Queued;
            JobFactory.StartNew(() =>
            {
                job.Status = Status.Running;
                switch (job.JobType)
                {
                    case JobType.Member:
                        doMemberJob(job);
                        break;
                    case JobType.Tags:
                        doSearchJob(job);
                        break;
                    case JobType.Image:
                        doImageJob(job);
                        break;
                }
                if (job.Status != Status.Error)
                    job.Status = Status.Completed;
            }
            );
        }

        private static void doImageJob(JobDownloadViewModel job)
        {
            try
            {
                NijieImage image = new NijieImage(job.ImageId);
                processImage(job, null, image);
            }
            catch (NijieException ne)
            {
                job.Status = Status.Error;
                job.Message = ne.Message;
            }
        }

        private static void doSearchJob(JobDownloadViewModel job)
        {
            try
            {
                job.CurrentPage = job.StartPage;
                int endPage = job.EndPage;
                int sort = job.Sort;
                int limit = job.Limit;
                bool flag = true;

                job.DownloadCount = 0;

                while (flag)
                {
                    job.Message = "Parsing search page: " + job.CurrentPage;
                    var searchPage = Bot.Search(job.SearchTag, job.CurrentPage, sort);

                    foreach (var image in searchPage.Images)
                    {
                        processImage(job, null, image);
                        ++job.DownloadCount;
                        if (job.DownloadCount > limit && limit != 0)
                        {
                            job.Message = "Image limit reached: " + limit;
                            return;
                        }
                    }

                    ++job.CurrentPage;
                    if (job.CurrentPage > endPage && endPage != 0)
                    {
                        job.Message = "Page limit reached: " + endPage;
                        return;
                    }
                    else if (job.DownloadCount < limit)
                    {
                        flag = searchPage.IsNextAvailable;
                    }
                }
            }
            catch (NijieException ne)
            {
                job.Status = Status.Error;
                job.Message = ne.Message;
            }
        }

        private static string makeFilename(NijieImage image, int currPage = 0)
        {
            var filename = Properties.Settings.Default.FilenameFormat;

            // {memberId} - {imageId}{page}{maxPage} - {tags}
            filename = filename.Replace("{memberId}", image.Member.MemberId.ToString());
            filename = filename.Replace("{imageId}", image.ImageId.ToString());

            if (image.IsManga)
            {
                filename = filename.Replace("{page}", currPage.ToString());
                filename = filename.Replace("{maxPage}", " of " + image.ImageUrls.Count);
            }
            else
            {
                filename = filename.Replace("{page}", "");
                filename = filename.Replace("{maxPage}", "");
            }

            if (image.Tags != null || image.Tags.Count > 0)
                filename = filename.Replace("{tags}", String.Join(" ", image.Tags));
            else
                filename = filename.Replace("{tags}", "");

            return filename;
        }

        private static void doMemberJob(JobDownloadViewModel job)
        {
            try
            {
                job.Message = "Parsing member page";
                var memberPage = Bot.ParseMember(job.MemberId);

                foreach (var imageTemp in memberPage.Images)
                {
                    processImage(job, memberPage, imageTemp);
                    ++job.DownloadCount;
                }
            }
            catch (NijieException ne)
            {
                job.Status = Status.Error;
                job.Message = ne.Message;
            }
        }

        private static void processImage(JobDownloadViewModel job, NijieMember memberPage, NijieImage imageTemp)
        {
            try
            {
                var rootPath = Properties.Settings.Default.RootDirectory;
                var image = Bot.ParseImage(imageTemp, memberPage);
                if (image.IsManga)
                {
                    for (int i = 0; i < image.ImageUrls.Count; ++i)
                    {
                        var filename = makeFilename(image, i);
                        job.Message = "Downloading: " + image.ImageUrls[i];
                        var pagefilename = filename + "_p" + i + "." + Util.ParseExtension(image.ImageUrls[i]);
                        pagefilename = rootPath + "\\" + Util.SanitizeFilename(pagefilename);

                        var download = false;

                        if (!File.Exists(pagefilename) || Properties.Settings.Default.Overwrite)
                            download = true;
                        else
                            job.Message = "Skipped, file exists: " + pagefilename;

                        if (download)
                        {
                            dowloadUrl(job, image.ImageUrls[i], image.Referer, pagefilename);                            
                        }
                    }
                }
                else
                {
                    var filename = makeFilename(image);
                    job.Message = "Downloading: " + image.BigImageUrl;
                    filename = filename + "." + Util.ParseExtension(image.BigImageUrl);
                    filename = rootPath + "\\" + Util.SanitizeFilename(filename);
                    dowloadUrl(job, image.BigImageUrl, image.ViewUrl, filename);
                }
            }
            catch (NijieException ne)
            {
                job.Status = Status.Error;
                job.Message = ne.Message;
            }
        }

        private static void dowloadUrl(JobDownloadViewModel job, string url, string referer, string filename)
        {
            int retry = 0;
            while (retry < 3)
            {
                try
                {
                    Bot.Download(url, referer, filename);
                    job.Message = "Saving to: " + filename;
                    break;
                }
                catch (Exception ex)
                {
                    ++retry;
                    for (int i = 0; i < 60; ++i)
                    {
                        job.Message = ex.Message + " retry: " + retry + " wait: " + i;
                        Thread.Sleep(1000);
                    }
                }
            }
        }
    }
}