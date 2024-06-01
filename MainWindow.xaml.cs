using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Discord;
using System.Threading;
using System.IO;

namespace BiliCord
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Stack<Uri> history = new Stack<Uri>();
        Discord.ActivityManager activityManager;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                InitialiseWebView();
                InitialiseDiscord();
            }
            catch (Exception e)
            {
                StreamWriter sw = new StreamWriter("error.txt");
                sw.WriteLine(e.Message);
                sw.WriteLine(e.StackTrace);
                sw.Close();
            }

        }

        private void InitialiseDiscord()
        {
            var clientID = Environment.GetEnvironmentVariable("DISCORD_CLIENT_ID");
            if (clientID == null)
            {
                clientID = "1245145932501356595";
            }
            var discord = new Discord.Discord(Int64.Parse(clientID), (UInt64)Discord.CreateFlags.Default);
            discord.RunCallbacks();
            discord.SetLogHook(Discord.LogLevel.Debug, LogProblemFunctions);
            var applicationManager = discord.GetApplicationManager();
            var userManager = discord.GetUserManager();
            var imageManager = discord.GetImageManager();

            userManager.OnCurrentUserUpdate += () =>
            {
                var currentUser = userManager.GetCurrentUser();
                Trace.WriteLine(currentUser.Username);
                Trace.WriteLine(currentUser.Id);
            };

            var activityManager = discord.GetActivityManager();
            var lobbyManager = discord.GetLobbyManager();
            this.activityManager = activityManager;

            activityManager.RegisterCommand();
            var activity = new Discord.Activity
            {
                State = "Watching Bilibili",
                Details = "Watching Bilibili",
            };
            Trace.WriteLine("Discord Rich Presence Initialised");
            activityManager.UpdateActivity(activity, (res) =>
            {
                if (res == Discord.Result.Ok)
                {
                    Trace.WriteLine("Discord Rich Presence Updated");
                }
                else
                {
                    Trace.WriteLine("Discord Rich Presence Update Failed");
                }
            });

            // Pump the event look to ensure all callbacks continue to get fired.
            Thread discordThread = new Thread(() => DiscordLoop(discord, lobbyManager));
            discordThread.IsBackground = true;
            discordThread.Start();

        }

        private void updateActivity(Discord.Activity activity)
        {
            this.activityManager.UpdateActivity(activity, (res) =>
            {
                if (res == Discord.Result.Ok)
                {
                    Trace.WriteLine("Discord Rich Presence Updated");
                }
                else
                {
                    Trace.WriteLine("Discord Rich Presence Update Failed");
                }
            });
        }

        private void DiscordLoop(Discord.Discord discord, LobbyManager lobbyManager)
        {

            try
            {
                while (true)
                {
                    discord.RunCallbacks();
                    lobbyManager.FlushNetwork();
                    Thread.Sleep(1000 / 60);
                }
            }
            finally
            {
                discord.Dispose();
            }
        }

        public void LogProblemFunctions(Discord.LogLevel level, string message)
        {
            String logLevel = level.ToString();
            String logMessage = message;
            Trace.WriteLine("Discord:{0} - {1}" + logLevel + logMessage);
        }

        public async void InitialiseWebView()
        {
            await webView.EnsureCoreWebView2Async(null);

            String url = webView.Source.ToString();
            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView.CoreWebView2.SourceChanged += CoreWebView2_SourceChanged;
            // listen for title change
            webView.CoreWebView2.DocumentTitleChanged += CoreWebView2_DocumentTitleChanged;
        }

        private void CoreWebView2_DocumentTitleChanged(object? sender, object e)
        {
            Update_Activity();
        }

        private void CoreWebView2_NewWindowRequested(object sender,
            CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.NewWindow = (CoreWebView2)sender;
            e.Handled = true;
        }

        private void CoreWebView2_SourceChanged(object sender, CoreWebView2SourceChangedEventArgs e)
        {
            Update_Activity();
        }

        private void Update_Activity()
        {
            String url = webView.Source.ToString();
            String title = webView.CoreWebView2.DocumentTitle;
            String BV = "";
            if (title == "")
            {
                title = "Watching Bilibili";
            }
            // conver string title to utf-8 encoding
            byte[] bytes = Encoding.Default.GetBytes(title);
            var new_title = Encoding.UTF8.GetString(bytes);
            new_title = new_title.Split("_哔哩哔哩")[0];

            // Find the index of the prefix
            int startIndex = url.IndexOf("BV");

            // If the prefix is found, extract the substring starting from the prefix
            if (startIndex != -1)
            {
                BV = url.Substring(startIndex);
                BV = BV.Split("/?")[0];
            }

            Trace.WriteLine("Title: " + new_title);
            var activity = new Discord.Activity
            {
                State = BV,
                Details = new_title,
            };
            updateActivity(activity);

            // push if not bili rewrite url
            if (history.Count == 0)
            {
                history.Push(new Uri(url));

            }
            else if (history.Peek().ToString().Split("/?")[0].StartsWith(url.Split("/?")[0]) == false)
            {
                history.Push(new Uri(url));
            }

            // print out stack
            foreach (Uri uri in history)
            {
                Trace.WriteLine(uri);
            }
        }

        void BrowseBack_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (history.Count > 1)
            {
                history.Pop(); // Pop the current page
                Uri uri = history.Pop(); // Pop the previous page
                webView.CoreWebView2.Navigate(uri.ToString());
            }
        }

        private void BrowseBack_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            // Enable the button only if the WebView can navigate back
            e.CanExecute = history.Count > 1;
        }

    }


}
