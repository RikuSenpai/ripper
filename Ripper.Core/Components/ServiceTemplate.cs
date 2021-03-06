// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ServiceTemplate.cs" company="The Watcher">
//   Copyright (c) The Watcher Partial Rights Reserved.
//   This software is licensed under the MIT license. See license.txt for details.
// </copyright>
// <summary>
//   Code Named: Ripper
//   Function  : Extracts Images posted on forums and attempts to fetch them to disk.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Ripper.Core.Components
{
    using System;
    using System.Collections;
    using System.ComponentModel;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Windows.Forms;

    using Ripper.Core.Objects;

    /// <summary>
    /// Service Template Class
    /// </summary>
    public abstract class ServiceTemplate
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceTemplate" /> class.
        /// </summary>
        /// <param name="savePath">The save path.</param>
        /// <param name="imageUrl">The image url.</param>
        /// <param name="thumbImageUrl">The thumb image URL.</param>
        /// <param name="postTitle">The post title</param>
        /// <param name="imageNumber">The image number.</param>
        /// <param name="hashTable">The url list.</param>
        protected ServiceTemplate(string savePath, string imageUrl, string thumbImageUrl, string postTitle, int imageNumber, ref Hashtable hashTable)
        {
            this.WebClient = new WebClient();
            this.WebClient.DownloadFileCompleted += this.DownloadImageCompleted;

            this.ImageLinkURL = imageUrl;
            this.ThumbImageURL = thumbImageUrl;
            this.EventTable = hashTable;
            this.SavePath = savePath;
            this.PostTitle = postTitle;
            this.ImageNumber = imageNumber;
        }

        /// <summary>
        /// Gets or sets the hashTable with URLs.
        /// </summary>
        protected Hashtable EventTable { get; set; }

        /// <summary>
        /// Gets or sets the Thumb Image Url
        /// </summary>
        protected string ThumbImageURL { get; set; }

        /// <summary>
        /// Gets or sets the Image Link Url
        /// </summary>
        protected string ImageLinkURL { get; set; }

        /// <summary>
        /// Gets or sets the Image Save Folder Path
        /// </summary>
        protected string SavePath { get; set; }

        /// <summary>
        /// Gets or sets the post title.
        /// </summary>
        /// <value>
        /// The post title.
        /// </value>
        protected string PostTitle { get; set; }

        /// <summary>
        /// Gets or sets the image number.
        /// </summary>
        /// <value>
        /// The image number.
        /// </value>
        protected int ImageNumber { get; set; }

        /// <summary>
        /// Gets or sets the web client.
        /// </summary>
        /// <value>
        /// The web client.
        /// </value>
        protected WebClient WebClient { get; set; }

        /// <summary>
        /// Start Download
        /// </summary>
        [Obsolete("Please use StartDownloadAsync instead.")]
        public void StartDownload()
        {
            this.DoDownload();

            this.RemoveThread();
        }

        /// <summary>
        /// Start Download Async.
        /// </summary>
        public void StartDownloadAsync()
        {
            if (this.EventTable.ContainsKey(this.ImageLinkURL))
            {
                return;
            }

            var cacheObject = new CacheObject { IsDownloaded = false, FilePath = string.Empty, Url = this.ImageLinkURL };

            try
            {
                this.EventTable.Add(this.ImageLinkURL, cacheObject);
            }
            catch (ThreadAbortException)
            {
                return;
            }
            catch (Exception)
            {
                if (this.EventTable.ContainsKey(this.ImageLinkURL))
                {
                    return;
                }

                this.EventTable.Add(this.ImageLinkURL, cacheObject);
            }

            try
            {
                if (!Directory.Exists(this.SavePath))
                {
                    Directory.CreateDirectory(this.SavePath);
                }
            }
            catch (IOException)
            {
               return;
            }

            if (!this.DoDownload())
            {
                this.RemoveThread();
            }
        }

        /// <summary>
        /// Do the Download
        /// </summary>
        /// <returns>
        /// Returns if the Image was downloaded
        /// </returns>
        protected abstract bool DoDownload();

        /// <summary>
        /// a generic function to fetch URLs.
        /// </summary>
        /// <param name="imageHostURL">The image host URL.</param>
        /// <param name="method">The method.</param>
        /// <param name="postData">The post data.</param>
        /// <returns>
        /// Returns the Page as string.
        /// </returns>
        protected string GetImageHostPage(ref string imageHostURL, string method, string postData)
        {
            string pageContent;

            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(imageHostURL);

                webRequest.Method = method;
                webRequest.ContentType = "application/x-www-form-urlencoded";
                webRequest.Referer = imageHostURL;
                webRequest.KeepAlive = true;
                webRequest.Timeout = 20000;

                using (var stream = webRequest.GetRequestStream())
                {
                    var buffer = Encoding.UTF8.GetBytes(postData);
                    stream.Write(buffer, 0, buffer.Length);
                }

                return GetResponseStream(webRequest);
            }
            catch (ThreadAbortException)
            {
                pageContent = string.Empty;
            }
            catch (Exception)
            {
                pageContent = string.Empty;
            }

            return pageContent;
        }

        /// <summary>
        /// a generic function to fetch URLs.
        /// </summary>
        /// <param name="imageHostURL">The image host URL.</param>
        /// <param name="cookieValue">The cookie.</param>
        /// <returns>
        /// Returns the Page as string.
        /// </returns>
        protected string GetImageHostPage(ref string imageHostURL, string cookieValue = null)
        {
            string pageContent;

            try
            {
                var webRequest = (HttpWebRequest)WebRequest.Create(imageHostURL);

                webRequest.Referer = imageHostURL;
                webRequest.KeepAlive = true;
                webRequest.Timeout = 20000;
                webRequest.CookieContainer = new CookieContainer();

                if (!string.IsNullOrEmpty(cookieValue))
                {
                    webRequest.Headers["Cookie"] = cookieValue;
                }

                return GetResponseStream(webRequest);
            }
            catch (ThreadAbortException ex)
            {
                pageContent = string.Empty;
            }
            catch (Exception ex)
            {
                pageContent = string.Empty;
            }

            return pageContent;
        }

        /// <summary>
        /// Gets the cookie value.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="matchString">The match.</param>
        /// <returns>
        /// Returns the Cookie Value
        /// </returns>
        protected string GetCookieValue(string url, string matchString)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);

                req.Referer = url;
                req.Timeout = 20000;

                var res = (HttpWebResponse)req.GetResponse();

                var stream = res.GetResponseStream();
                if (stream != null)
                {
                    var reader = new StreamReader(stream);

                    var page = reader.ReadToEnd();

                    res.Close();
                    reader.Close();

                    var match = Regex.Match(page, matchString, RegexOptions.Compiled);

                    return match.Success ? match.Groups["inner"].Value : string.Empty;
                }
            }
            catch (ThreadAbortException)
            {
                return string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the name of the image.
        /// </summary>
        /// <param name="postTitle">The post title.</param>
        /// <param name="imageUrl">The image URL.</param>
        /// <param name="imageNumber">The image number.</param>
        /// <returns>
        /// Returns the Image Name.
        /// </returns>
        protected string GetImageName(string postTitle, string imageUrl, int imageNumber)
        {
            postTitle = Utility.RemoveIllegalCharecters(postTitle).Replace(" ", "_");

            var imageExtension = imageUrl.Contains("attachment.php")
                                     ? ".jpg"
                                     : imageUrl.Substring(imageUrl.LastIndexOf(".", StringComparison.Ordinal));

            var imageName = $"{postTitle}_{imageNumber}{imageExtension}";

            // Check if folder path is too long
            var savePath = Path.Combine(this.SavePath, Utility.RemoveIllegalCharecters(imageName));
            
            if (savePath.Length > 250)
            {
                return $"{imageNumber}{imageExtension}";
            }

            return imageName;
        }

        /// <summary>
        /// Downloads the image.
        /// </summary>
        /// <param name="downloadPath">The download path.</param>
        /// <param name="savePath">The save path.</param>
        /// <param name="addReferer">if set to <c>true</c> [add Referrer].</param>
        /// <param name="addForumCookie">if set to <c>true</c> [add forum cookie].</param>
        /// <returns>
        /// Returns if the Image was downloaded or not
        /// </returns>
        protected bool DownloadImageAsync(string downloadPath, string savePath, bool addReferer = false, bool addForumCookie = false)
        {
            savePath = Path.Combine(this.SavePath, Utility.RemoveIllegalCharecters(savePath));

            if (!Directory.Exists(this.SavePath))
            {
                Directory.CreateDirectory(this.SavePath);
            }

            savePath = Utility.GetSuitableName(savePath);

            ((CacheObject)this.EventTable[this.ImageLinkURL]).FilePath = savePath;

            if (addReferer)
            {
                this.WebClient.Headers.Add($"Referer: {this.ThumbImageURL}"); 
            }

            if (addForumCookie)
            {
                this.WebClient.Headers.Add(HttpRequestHeader.Cookie, CookieManager.GetInstance().GetCookieString());
            }

            Application.DoEvents();

            this.WebClient.DownloadFileAsync(new Uri(downloadPath), savePath);

            return true;
        }

        /// <summary>
        /// Removes the thread.
        /// </summary>
        protected void RemoveThread()
        {
            if (!this.EventTable.Contains(this.ImageLinkURL))
            {
                return;
            }

            this.EventTable.Remove(this.ImageLinkURL);
            ThreadManager.GetInstance().RemoveThreadbyId(this.ImageLinkURL);
        }

        /// <summary>
        /// Gets the response stream.
        /// </summary>
        /// <param name="webRequest">The web request.</param>
        /// <returns>Returns the Response Stream</returns>
        private static string GetResponseStream(WebRequest webRequest)
        {
            string pageContent;

            var responseStream = webRequest.GetResponse().GetResponseStream();

            if (responseStream != null)
            {
                var reader = new StreamReader(responseStream);

                pageContent = reader.ReadToEnd();

                responseStream.Close();
                reader.Close();
            }
            else
            {
                responseStream.Close();
                return string.Empty;
            }

            return pageContent;
        } 
        

        /// <summary>
        /// Downloads the image completed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.ComponentModel.AsyncCompletedEventArgs" /> instance containing the event data.</param>
        private void DownloadImageCompleted(object sender, AsyncCompletedEventArgs e)
        {
            var cacheObject = (CacheObject)this.EventTable[this.ImageLinkURL];

            if (e.Error == null)
            {
                ((CacheObject)this.EventTable[this.ImageLinkURL]).IsDownloaded = true;

                Application.DoEvents();

                CacheController.Instance().LastPic = cacheObject.FilePath;
            }
            else
            {
                // Delete empty files
                /*if (File.Exists(cacheObject.FilePath))
                {
                    File.Delete(cacheObject.FilePath);
                }*/
                ((CacheObject)this.EventTable[this.ImageLinkURL]).IsDownloaded = false;
            }

            this.RemoveThread();

            Application.DoEvents();

            this.WebClient.Dispose();
        }       
    }
}