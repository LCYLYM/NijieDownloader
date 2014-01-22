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

using NijieDownloader.Library.Model;
using FirstFloor.ModernUI.Windows.Navigation;
using NijieDownloader.UI.ViewModel;
using System.Diagnostics;

namespace NijieDownloader.UI
{
    /// <summary>
    /// Interaction logic for Page1.xaml
    /// </summary>
    public partial class MemberPage : Page
    {
        public NijieMemberViewModel ViewData { get; set; }

        public MemberPage()
        {
            InitializeComponent();
        }


        private void btnFetch_Click(object sender, RoutedEventArgs e)
        {
            var result = MainWindow.Bot.ParseMember(Int32.Parse(txtMemberID.Text));
            ViewData = new NijieMemberViewModel(result);
            this.DataContext = ViewData;
        }

        private void StackPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (lbxImages.SelectedIndex > -1 && lbxImages.SelectedIndex < ViewData.Images.Count)
            {
                var uri = new Uri("/ImagePage.xaml#ImageId=" + ViewData.Images[lbxImages.SelectedIndex].Image.ImageId, UriKind.RelativeOrAbsolute);
                var frame = NavigationHelper.FindFrame(null, this);
                if (frame != null)
                {
                    frame.Source = uri;
                }
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            lbxImages.MaxHeight = e.NewSize.Height;
        }

        private void btnAddToBatch_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtMemberID.Text))
            {
                var uri = new Uri("/BatchDownloadPage.xaml#type=member&memberId=" + txtMemberID.Text, UriKind.RelativeOrAbsolute);
                var frame = NavigationHelper.FindFrame(null, this);
                if (frame != null)
                {
                    frame.Source = uri;
                }
            }
        }        
    }
}
