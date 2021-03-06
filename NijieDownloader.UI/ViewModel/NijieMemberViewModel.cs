﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media.Imaging;
using NijieDownloader.Library;
using NijieDownloader.Library.Model;

namespace NijieDownloader.UI.ViewModel
{
    public class NijieMemberViewModel : ViewModelBase
    {
        private NijieMember _member;

        #region ctor

        public NijieMemberViewModel()
        {
        }

        public NijieMemberViewModel(NijieMember member)
        {
            _member = member;
            this.MemberId = member.MemberId;
            this.Mode = (MemberMode)member.Mode;
        }

        #endregion ctor

        #region properties

        private int _memberId;

        public int MemberId
        {
            get
            {
                return _memberId;
            }
            set
            {
                _memberId = value;
                onPropertyChanged("MemberId");
                onPropertyChanged("MemberUrl");
            }
        }

        private string _avatarImageStatus;
        private BitmapImage _avatarImage;

        public BitmapImage AvatarImage
        {
            get
            {
                if (_avatarImage == null)
                {
                    var loading = ViewModelHelper.NoAvatar;
                    if (_member != null && _avatarImageStatus != ImageLoader.IMAGE_LOADING)
                    {
                        loading = ViewModelHelper.Loading;
                        _avatarImageStatus = ImageLoader.IMAGE_LOADING;
                        ImageLoader.LoadImage(_member.AvatarUrl, _member.MemberUrl,
                            new Action<BitmapImage, string>((image, status) =>
                            {
                                this.AvatarImage = null;
                                this.AvatarImage = image;
                                _avatarImageStatus = status;
                            }
                        ));
                    }
                    return loading;
                }
                return _avatarImage;
            }
            set
            {
                _avatarImage = value;
                onPropertyChanged("AvatarImage");
            }
        }

        private MemberMode _mode;

        public MemberMode Mode
        {
            get
            {
                return _mode;
            }
            set
            {
                _mode = value;
                if (_member != null)
                    _member.Mode = value;
                onPropertyChanged("Mode");
                onPropertyChanged("MemberUrl");
            }
        }

        private int _page = 1;

        public int Page
        {
            get { return _page; }
            set
            {
                _page = value;
                onPropertyChanged("Page");
                onPropertyChanged("MemberUrl");
            }
        }

        public bool IsNextPageAvailable
        {
            get
            {
                if (_member != null) return _member.IsNextAvailable;
                return false;
            }
        }

        private ObservableCollection<NijieImageViewModel> _images;

        public ObservableCollection<NijieImageViewModel> Images
        {
            get
            {
                return _images;
            }
            set
            {
                _images = value;
                onPropertyChanged("Images");
            }
        }

        private string _status;

        public string Status
        {
            get { return _status; }
            set
            {
                _status = value;
                onPropertyChanged("Status");
            }
        }

        private string _username;
        public string UserName
        {
            get 
            { 
                return _username;
            }
            set
            {
                _username = value;
                onPropertyChanged("UserName");
            }
        }

        public string MemberUrl
        {
            get
            {
                //if (_member != null) return _member.MemberUrl;
                return NijieMember.GenerateMemberUrl(MemberId, Mode, Page);
            }
        }

        private bool _isSelected;

        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }
            set
            {
                _isSelected = value;
                onPropertyChanged("IsSelected");
            }
        }

        public int TotalImages
        {
            get
            {
                if (_member != null)
                {
                    return _member.TotalImages;
                }
                return -1;
            }
        }

        #endregion properties

        public void GetMember(SynchronizationContext context)
        {
            try
            {
                _member = MainWindow.Bot.ParseMember(this.MemberId, this.Mode, this.Page);
                this.UserName = _member.UserName;

                ImageLoader.LoadImage(_member.AvatarUrl, _member.MemberUrl,
                            new Action<BitmapImage, string>((image, status) =>
                            {
                                this.AvatarImage = null;
                                this.AvatarImage = image;
                                _avatarImageStatus = status;
                            }
                        ));

                if (_member.Images != null)
                {
                    Images = new ObservableCollection<NijieImageViewModel>();
                    foreach (var image in _member.Images)
                    {
                        var temp = new NijieImageViewModel(image);
                        context.Send((x) =>
                        {
                            Images.Add(temp);
                        }, null);
                    }

                    this.Status = String.Format("Loaded: {0} images.", _member.Images.Count);
                    onPropertyChanged("TotalImages");
                    this.HasError = false;
                }
            }
            catch (NijieException ne)
            {
                MainWindow.Log.Error(ne.Message, ne);

                this.UserName = null;
                this.AvatarImage = ViewModelHelper.NoAvatar;
                context.Send((x) =>
                    {
                        if (Images != null)
                        {
                            Images.Clear();
                            Images = null;
                        }
                    }, null);

                this.HasError = true;
                this.Status = "[Error] " + ne.Message;
            }
        }
    }
}