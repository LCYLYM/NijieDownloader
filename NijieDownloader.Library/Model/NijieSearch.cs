﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Nandaka.Common;

namespace NijieDownloader.Library.Model
{
    public class NijieSearch
    {
        public NijieSearch(NijieSearchOption option)
        {
            this.Option = option;
        }

        public NijieSearchOption Option { get; set; }

        public string QueryUrl
        {
            get
            {
                return GenerateQueryUrl(Option);
            }
            private set { }
        }

        public List<NijieImage> Images { get; set; }

        public bool IsNextAvailable { get; set; }

        public bool IsPrevAvailable
        {
            get
            {
                if (Option != null)
                    return Option.Page > 1;
                return false;
            }
        }

        public static string GenerateQueryUrl(NijieSearchOption option)
        {
            var url = String.Format(@"//nijie.info/search.php?type={0}&word={1}&p={2}&mode={3}&illust_type={4}&sort={5}"
                    , option.SearchTypeStr
                    , option.Query
                    , (int)option.Page
                    , (int)option.SearchBy
                    , ""
                    , (int)option.Sort);
            return Util.FixUrl(url, Nijie.ROOT_DOMAIN, Properties.Settings.Default.UseHttps);
        }

        public int TotalImages { get; set; }
    }
}